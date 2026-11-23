"""Exercise SQL bootstrap and application permissions against real containers in CI."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile


root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("configuration", root / "deployment/configure.py")
configuration = importlib.util.module_from_spec(spec)
spec.loader.exec_module(configuration)

with tempfile.TemporaryDirectory() as directory:
    bundle = Path(directory)
    shutil.copyfile(root / "docker-compose.prod.yml", bundle / "compose.yml")
    network = "indotel-sql-check-" + bundle.name.lower().replace("_", "-")
    (bundle / ".env").write_text(
        "APP_IMAGE=ghcr.io/killermaker/check:unused\nPUBLIC_DOMAIN=api.example.com\n"
        "PORTAL_DOMAIN=portal.example.com\nCOMPOSE_PROJECT_NAME=" + network + "\nDOCKER_NETWORK=" + network + "\n",
        encoding="utf-8",
    )
    # Disposable test credentials, deliberately including SQL and Compose metacharacters.
    admin_password = "Ci_Admin_Password123!"
    app_password = "Ci_App';$Password123!"
    (bundle / "sql-init.sql").write_text(configuration.sql_bootstrap(app_password), encoding="utf-8")
    (bundle / "runtime.json").write_text(json.dumps({"services": {
        "sqlserver": {"environment": {"MSSQL_SA_PASSWORD": admin_password}, "ports": ["127.0.0.1::1433"]},
        "sql-init": {"environment": {"SQLCMDPASSWORD": admin_password}},
    }}), encoding="utf-8")
    compose = ["docker", "compose", "--env-file", str(bundle / ".env"), "-f", str(bundle / "compose.yml"), "-f", str(bundle / "runtime.json")]
    subprocess.run(["docker", "network", "create", network], check=True, stdout=subprocess.DEVNULL)
    try:
        subprocess.run([*compose, "up", "-d", "--wait", "--wait-timeout", "240", "sqlserver"], check=True, timeout=300)
        subprocess.run([*compose, "run", "--rm", "--no-deps", "sql-init"], check=True, timeout=60)
        # Run bootstrap twice to verify repeat deployments are safe.
        subprocess.run([*compose, "run", "--rm", "--no-deps", "sql-init"], check=True, timeout=60)
        import os
        environment = os.environ.copy()
        environment["SQLCMDPASSWORD"] = app_password
        subprocess.run([*compose, "run", "--rm", "--no-deps", "-e", "SQLCMDPASSWORD", "sql-init",
                        "-S", "sqlserver", "-U", "indotel_api", "-d", "Indotel", "-C", "-b", "-Q",
                        "IF IS_SRVROLEMEMBER('sysadmin') <> 0 OR HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONTROL') <> 0 THROW 50000, 'Application login has excessive privileges', 1; SELECT 1;"],
                       env=environment, check=True, timeout=60)
        endpoint = subprocess.check_output([*compose, "port", "sqlserver", "1433"], text=True).strip()
        port = endpoint.rsplit(":", 1)[1]
        environment["SQL_CHECK_CONNECTION_STRING"] = (
            f'Server=127.0.0.1,{port};Database=Indotel;User Id=indotel_api;Password="{app_password}";Encrypt=True;TrustServerCertificate=True;'
        )
        subprocess.run(["dotnet", "run", "--project", str(root / "Tests/SqlChecks/SqlChecks.csproj"), "--configuration", "Release"],
                       env=environment, check=True, timeout=180)
        original_id = subprocess.check_output([*compose, "ps", "-q", "sqlserver"], text=True).strip()
        subprocess.run([*compose, "up", "-d", "--no-deps", "--no-recreate", "--wait", "sqlserver"], check=True, timeout=120)
        retained_id = subprocess.check_output([*compose, "ps", "-q", "sqlserver"], text=True).strip()
        assert retained_id == original_id, "Normal deployment must retain the SQL container"
        subprocess.run([*compose, "up", "-d", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "240", "sqlserver"], check=True, timeout=300)
        replacement_id = subprocess.check_output([*compose, "ps", "-q", "sqlserver"], text=True).strip()
        assert replacement_id != original_id, "Explicit recreation must replace the SQL container"
        subprocess.run([*compose, "run", "--rm", "--no-deps", "-e", "SQLCMDPASSWORD", "sql-init",
                        "-S", "sqlserver", "-U", "indotel_api", "-d", "Indotel", "-C", "-b", "-Q",
                        "IF NOT EXISTS (SELECT 1 FROM dbo.Client WHERE NationalIdentificationNumber = N'001-1234567-0') THROW 50000, 'Client data was lost during container recreation', 1;"],
                       env=environment, check=True, timeout=60)
        print("SQL schema, repositories, retained container and recreation data-persistence checks passed.")
    finally:
        subprocess.run([*compose, "down", "--volumes"], check=True)
        subprocess.run(["docker", "network", "rm", network], check=True, stdout=subprocess.DEVNULL)
