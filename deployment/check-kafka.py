"""Exercise the production broker in a disposable, isolated Compose project."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import uuid


root = Path(__file__).resolve().parents[1]
env = dict(os.environ, APP_IMAGE="unused", PUBLIC_DOMAIN="api.example.test", PORTAL_DOMAIN="portal.example.test")
rendered = json.loads(subprocess.check_output(
    ["docker", "compose", "-f", str(root / "docker-compose.prod.yml"), "config", "--format", "json"], env=env))
broker = rendered["services"]["kafka"]
assert not broker.get("ports"), "Kafka must not publish public ports"
broker["networks"] = {"default": None}
with tempfile.TemporaryDirectory(prefix="indotel-kafka-check-") as directory:
    config = Path(directory) / "compose.json"
    config.write_text(json.dumps({"services": {"kafka": broker}, "volumes": {"kafka_data": {}}}), encoding="utf-8")
    compose = ["docker", "compose", "-p", "indotel-kafka-check-" + uuid.uuid4().hex[:10], "-f", str(config)]

    def run(*args, **kwargs):
        return subprocess.run(compose + list(args), check=True, **kwargs)

    try:
        run("up", "-d", "--wait", "--wait-timeout", "180")
        run("exec", "-T", "kafka", "/opt/kafka/bin/kafka-topics.sh", "--bootstrap-server", "kafka:9092",
            "--create", "--topic", "deployment-check", "--partitions", "1", "--replication-factor", "1")
        run("exec", "-T", "kafka", "/opt/kafka/bin/kafka-console-producer.sh", "--bootstrap-server", "kafka:9092",
            "--topic", "deployment-check", input="persistent-test-message\n", text=True)
        run("up", "-d", "--force-recreate", "--wait", "--wait-timeout", "180", "kafka")
        received = run("exec", "-T", "kafka", "/opt/kafka/bin/kafka-console-consumer.sh", "--bootstrap-server",
                       "kafka:9092", "--topic", "deployment-check", "--from-beginning", "--max-messages", "1",
                       "--timeout-ms", "30000", capture_output=True, text=True)
        assert "persistent-test-message" in received.stdout
        print("Kafka checks passed: private broker, delivery, and persisted messages after container recreation.")
    finally:
        # Only the unique disposable test project and its test volume are removed.
        run("down", "--volumes", "--remove-orphans")
