"""Provision GitHub Environment settings from the deployment manifest using gh."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess


def manifest():
    items = json.loads(Path(__file__).with_name("settings.schema.json").read_text(encoding="utf-8"))
    items += [{"source": name, "kind": "secret", "required": True} for name in ["MSSQL_SA_PASSWORD", "SQL_APP_PASSWORD"]]
    extra = {
        "DROPLET_HOST": None, "DROPLET_USER": "deploy", "DROPLET_SSH_PORT": "22",
        "DROPLET_SSH_KNOWN_HOSTS": "", "DEPLOY_PATH": "/opt/exatech-indotel/api",
        "APP_PORT": "8080", "PUBLIC_DOMAIN": None, "PORTAL_DOMAIN": None,
        "DOCKER_NETWORK": "exatech-indotel", "COMPOSE_PROJECT_NAME": "exatech-indotel-api",
        "GHCR_USERNAME": "KillerMaker", "DROPLET_SSH_KEY": None, "GHCR_TOKEN": None,
    }
    for name, default in extra.items():
        items.append({"source": name, "kind": "secret" if name in ["DROPLET_SSH_KEY", "GHCR_TOKEN"] else "variable",
                      "required": default is None, "default": default})
    return items


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--environment", choices=["Staging", "Production"])
    parser.add_argument("--repo", default="KillerMaker/Extech-Indotel-Api")
    parser.add_argument("--values", type=Path, help="Private JSON object keyed by GitHub setting names")
    parser.add_argument("--write-template", type=Path)
    parser.add_argument("--apply", action="store_true", help="Upload settings; otherwise only validate")
    args = parser.parse_args()
    items = manifest()
    if args.write_template:
        # Exclusive creation protects an existing populated secrets file.
        with args.write_template.open("x", encoding="utf-8") as target:
            json.dump({item["source"]: item.get("default") for item in items}, target, ensure_ascii=False, indent=2)
        args.write_template.chmod(0o600)
        print("Created settings template. Fill required null values locally; keep this file private.")
        return
    if not args.values or not args.environment:
        parser.error("--values and --environment are required")
    supplied = json.loads(args.values.read_text(encoding="utf-8-sig"))
    if not isinstance(supplied, dict):
        raise ValueError("Settings must be a JSON object")
    unknown = set(supplied) - {item["source"] for item in items}
    if unknown:
        raise ValueError("Unknown settings: " + ", ".join(sorted(unknown)))
    values = []
    missing = []
    for item in items:
        value = supplied.get(item["source"], item.get("default"))
        if value is None or value == "":
            if item["required"]:
                missing.append(item["source"])
            continue
        if isinstance(value, list):
            value = json.dumps(value, ensure_ascii=False)
        if not isinstance(value, str):
            raise ValueError(item["source"] + " must be a string or JSON array")
        values.append((item, value))
    if missing:
        raise ValueError("Missing required settings: " + ", ".join(missing))
    if not args.apply:
        print("Validated " + str(len(values)) + " settings. Use --apply to upload. Values were not printed.")
        return
    if not shutil.which("gh"):
        raise ValueError("Install GitHub CLI and run gh auth login with repository administration access first.")
    subprocess.run(["gh", "api", "--method", "PUT", "repos/" + args.repo + "/environments/" + args.environment],
                   check=True, stdout=subprocess.DEVNULL)
    for item, value in values:
        command = ["gh", item["kind"], "set", item["source"], "--repo", args.repo, "--env", args.environment]
        # Send both types over stdin, keeping credentials out of command arguments.
        subprocess.run(command, input=value.encode("utf-8"), check=True, stdout=subprocess.DEVNULL)
        print("Uploaded " + item["kind"] + ": " + item["source"])
    print("Empty optional values were skipped; existing remote settings for those names were preserved.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, subprocess.CalledProcessError) as error:
        raise SystemExit(str(error))
