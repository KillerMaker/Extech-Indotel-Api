"""Upload a validated release bundle and deploy it over SSH."""
import argparse
import os
from pathlib import Path
import re
import shlex
import subprocess
import tempfile


def required(name):
    value = os.environ.get(name, "")
    if not value:
        raise ValueError("Missing setting: " + name)
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle", required=True)
    parser.add_argument("--files", nargs="+", required=True)
    args = parser.parse_args()
    host = required("DROPLET_HOST")
    user = required("DROPLET_USER")
    port = os.environ.get("DROPLET_SSH_PORT") or "22"
    target_path = required("DEPLOY_PATH")
    username = required("GHCR_USERNAME")
    token = required("GHCR_TOKEN")
    private_key = required("DROPLET_SSH_KEY")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9.-]*", host):
        raise ValueError("DROPLET_HOST must be an IPv4 address or DNS hostname")
    if not re.fullmatch(r"[A-Za-z0-9_-]+", user) or not re.fullmatch(r"[A-Za-z0-9_-]+", username):
        raise ValueError("Invalid SSH or GHCR username")
    if not port.isdigit() or not 1 <= int(port) <= 65535:
        raise ValueError("Invalid DROPLET_SSH_PORT")
    if not re.fullmatch(r"/[A-Za-z0-9_./-]+", target_path) or ".." in Path(target_path).parts or target_path == "/":
        raise ValueError("DEPLOY_PATH must be an absolute application directory without spaces or parent traversal")
    files = [(Path(source), name) for source, name in (item.split(":", 1) for item in args.files)]
    files += [(path, path.name) for path in Path(args.bundle).iterdir() if path.is_file()]
    for source, name in files:
        if not source.is_file() or not re.fullmatch(r"[A-Za-z0-9_.-]+", name):
            raise ValueError("Invalid release file: " + str(source))

    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        key = root / "id_ed25519"
        key.write_text(private_key.rstrip() + "\n", encoding="utf-8")
        key.chmod(0o600)
        known_hosts = root / "known_hosts"
        known = os.environ.get("DROPLET_SSH_KNOWN_HOSTS", "")
        if not known:
            known = subprocess.check_output(["ssh-keyscan", "-p", port, "-H", host], text=True)
        if not known.strip():
            raise ValueError("The droplet SSH host key could not be read")
        known_hosts.write_text(known.rstrip() + "\n", encoding="utf-8")
        options = ["-i", str(key), "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes",
                   "-o", "UserKnownHostsFile=" + str(known_hosts), "-o", "ConnectTimeout=20",
                   "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=4"]
        ssh = ["ssh", *options, "-p", port, user + "@" + host]
        quoted_path = shlex.quote(target_path)
        subprocess.run([*ssh, "mkdir -p " + quoted_path + " && chmod 700 " + quoted_path], check=True)
        for source, name in files:
            subprocess.run(["scp", *options, "-P", port, str(source),
                            user + "@" + host + ":" + target_path + "/" + name], check=True)
        subprocess.run([*ssh, "docker login ghcr.io -u " + shlex.quote(username) + " --password-stdin"],
                       input=token + "\n", text=True, check=True)
        subprocess.run([*ssh, "cd " + quoted_path + " && chmod 600 .env && bash ./deploy.sh"], check=True)


if __name__ == "__main__":
    try:
        main()
    except ValueError as error:
        raise SystemExit(str(error))
