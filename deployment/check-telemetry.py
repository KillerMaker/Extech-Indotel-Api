"""Verify actual OTLP HTTP exports from the API against a local test receiver."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import threading
import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

root = Path(__file__).resolve().parents[1]
received = []


class Receiver(BaseHTTPRequestHandler):
    def do_POST(self):
        payload = self.rfile.read(int(self.headers.get("Content-Length", "0")))
        received.append((self.path, self.headers.get("Authorization"), payload))
        self.send_response(200)
        self.send_header("Content-Type", "application/x-protobuf")
        self.end_headers()

    def log_message(self, *args):
        pass


receiver = ThreadingHTTPServer(("127.0.0.1", 0), Receiver)
threading.Thread(target=receiver.serve_forever, daemon=True).start()
with socket.socket() as allocation:
    allocation.bind(("127.0.0.1", 0))
    port = allocation.getsockname()[1]
env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Production", DOTNET_ENVIRONMENT="Production",
           ASPNETCORE_URLS=f"http://127.0.0.1:{port}", SecretKey="test-only-signing-key-at-least-32-bytes",
           ScheduledJob__Enabled="false", OTEL_SERVICE_NAME="indotel-telemetry-check",
           OTEL_EXPORTER_OTLP_ENDPOINT=f"http://127.0.0.1:{receiver.server_port}",
           OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf", OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer%20test-only-token",
           OTEL_METRIC_EXPORT_INTERVAL="1000")
with tempfile.TemporaryFile(mode="w+", encoding="utf-8") as output:
    process = subprocess.Popen(["dotnet", str(root / "bin/Release/net10.0/Exatech-Indotel-API.dll")],
                               cwd=root, env=env, stdout=output, stderr=subprocess.STDOUT)
    try:
        base = f"http://127.0.0.1:{port}"
        for attempt in range(100):
            if process.poll() is not None:
                raise RuntimeError("API exited before readiness")
            try:
                with urlopen(base + "/health", timeout=1) as response:
                    assert len(response.headers["X-Trace-Id"]) == 32
                break
            except (URLError, TimeoutError):
                time.sleep(0.2)
        else:
            raise RuntimeError("API did not become ready")
        try:
            urlopen(base + "/missing?documentNumber=private-test-cedula", timeout=3)
        except HTTPError as error:
            assert error.code == 404
            assert len(error.headers["X-Trace-Id"]) == 32
        for attempt in range(80):
            if {"/v1/logs", "/v1/traces", "/v1/metrics"}.issubset({item[0] for item in received}):
                break
            time.sleep(0.5)
        else:
            raise AssertionError("Missing OTLP signals: " + str({item[0] for item in received}))
        assert all(item[1] == "Bearer test-only-token" for item in received)
        assert all(b"private-test-cedula" not in item[2] for item in received)
        assert any(b"indotel-telemetry-check" in item[2] for item in received)
        print("Telemetry checks passed: actual logs, traces, metrics, authenticated OTLP, trace headers, and URL redaction.")
    except Exception:
        output.seek(0)
        print(output.read())
        raise
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        receiver.shutdown()
        receiver.server_close()
