"""Render Docker Compose configuration using GitHub Environment values."""
import argparse
import json
import os
from pathlib import Path
import re
import ipaddress
from urllib.parse import urlparse


def settings():
    result = {}
    schema = Path(__file__).with_name("settings.schema.json")
    for item in json.loads(schema.read_text(encoding="utf-8")):
        value = os.environ.get(item["source"], "")
        if item["kind"] != "secret":
            value = value.strip()
        if not value:
            value = item.get("default", "")
        if item["required"] and not value:
            raise ValueError("Missing setting: " + item["source"])
        if item["source"] == "CORS_ALLOWED_ORIGINS" and not value:
            value = ["https://" + domain("PORTAL_DOMAIN")]
        if item.get("array"):
            value = json.loads(value) if isinstance(value, str) else value
            if not isinstance(value, list) or not value or any(not isinstance(v, str) or not v.strip() for v in value):
                raise ValueError(item["source"] + " must be a nonempty JSON array of strings")
            for index, entry in enumerate(value):
                result[item["name"] + "__" + str(index)] = entry
        else:
            result[item["name"]] = str(value)
    if result["ASPNETCORE_ENVIRONMENT"] != result["DOTNET_ENVIRONMENT"]:
        raise ValueError("ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT must match")
    if len(result["SecretKey"].encode()) < 32:
        raise ValueError("JWT_SECRET_KEY must contain at least 32 UTF-8 bytes")
    for name in ["WisproClientsUrl", "WisproContractsUrl", "Siuben__Phase1__Url", "Siuben__Phase2__Url"]:
        url = urlparse(result[name])
        if url.scheme != "https" or not url.netloc or url.username or url.password:
            raise ValueError(name + " must be an HTTPS URL without embedded credentials")
    origins = [value for name, value in result.items() if name.startswith("Cors__AllowedOrigins__")]
    for origin in origins:
        url = urlparse(origin)
        if url.scheme != "https" or not url.netloc or url.path or url.query or url.fragment or url.username or url.password:
            raise ValueError("CORS_ALLOWED_ORIGINS must contain HTTPS origins without paths or trailing slashes")
    if result["EmailQueue__SecurityProtocol"] not in ["Plaintext", "Ssl", "SaslPlaintext", "SaslSsl"]:
        raise ValueError("Invalid EMAIL_KAFKA_SECURITY_PROTOCOL")
    if result["EmailQueue__SecurityProtocol"].startswith("Sasl") and any(
        not result[name] for name in ["EmailQueue__SaslMechanism", "EmailQueue__SaslUsername", "EmailQueue__SaslPassword"]
    ):
        raise ValueError("SASL requires a mechanism, username and password")
    if result["EmailQueue__SecurityProtocol"].startswith("Sasl") and result["EmailQueue__SaslMechanism"] not in ["Plain", "ScramSha256", "ScramSha512"]:
        raise ValueError("Supported SASL mechanisms are Plain, ScramSha256 and ScramSha512")
    return result


def domain(name):
    value = os.environ.get(name, "").strip()
    if not re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?", value) or "." not in value:
        raise ValueError(name + " must be a DNS hostname without a scheme, path or port")
    return value


def deployment():
    values = {
        "APP_IMAGE": os.environ["IMAGE_REF"],
        "PUBLIC_DOMAIN": domain("PUBLIC_DOMAIN"),
        "PORTAL_DOMAIN": domain("PORTAL_DOMAIN"),
        "COMPOSE_PROJECT_NAME": os.environ.get("COMPOSE_PROJECT_NAME") or "exatech-indotel-api",
        "DOCKER_NETWORK": os.environ.get("DOCKER_NETWORK") or "exatech-indotel",
        "DOCKER_SUBNET": os.environ.get("DOCKER_SUBNET") or "172.29.20.0/24",
        "APP_PORT": os.environ.get("APP_PORT") or "8080",
        "SQL_BIND_ADDRESS": os.environ.get("SQL_BIND_ADDRESS") or "127.0.0.1",
        "SQL_PORT": os.environ.get("SQL_PORT") or "1433",
        "RECREATE_DATABASE_CONTAINER": os.environ.get("RECREATE_DATABASE_CONTAINER") or "false",
    }
    if not re.fullmatch(r"ghcr\.io/[a-z0-9_./-]+:[a-zA-Z0-9_.-]+", values["APP_IMAGE"]):
        raise ValueError("IMAGE_REF must be a tagged ghcr.io image")
    for name in ["COMPOSE_PROJECT_NAME", "DOCKER_NETWORK"]:
        if not re.fullmatch(r"[a-z0-9][a-z0-9_-]*", values[name]):
            raise ValueError(name + " contains unsupported characters")
    if not 1024 <= int(values["APP_PORT"]) <= 65535:
        raise ValueError("APP_PORT must be between 1024 and 65535")
    ipaddress.IPv4Address(values["SQL_BIND_ADDRESS"])
    if not values["SQL_PORT"].isascii() or not values["SQL_PORT"].isdecimal() or not 1024 <= int(values["SQL_PORT"]) <= 65535:
        raise ValueError("SQL_PORT must be between 1024 and 65535")
    if int(values["SQL_PORT"]) == int(values["APP_PORT"]):
        raise ValueError("SQL_PORT must differ from APP_PORT")
    ipaddress.IPv4Network(values["DOCKER_SUBNET"])
    if values["RECREATE_DATABASE_CONTAINER"] not in ["true", "false"]:
        raise ValueError("RECREATE_DATABASE_CONTAINER must be true or false")
    return values


def escape_dollars(values):
    # Compose interpolates values in JSON override files as well as YAML.
    return {key: value.replace("$", "$$") for key, value in values.items()}


def sql_configuration():
    passwords = {}
    for name in ["MSSQL_SA_PASSWORD", "SQL_APP_PASSWORD"]:
        value = os.environ.get(name, "")
        categories = [r"[A-Z]", r"[a-z]", r"[0-9]", r"[^A-Za-z0-9]"]
        if not 12 <= len(value) <= 128 or sum(bool(re.search(pattern, value)) for pattern in categories) < 3:
            raise ValueError(name + " must have 12-128 characters and at least three character categories")
        passwords[name] = value
    if passwords["MSSQL_SA_PASSWORD"] == passwords["SQL_APP_PASSWORD"]:
        raise ValueError("SQL administrator and application passwords must differ")
    return passwords


def sql_bootstrap(password):
    # Disable sqlcmd variable substitution with -x; quote SQL string literals here.
    literal = password.replace("'", "''")
    bootstrap = """IF DB_ID(N'Indotel') IS NULL CREATE DATABASE [Indotel];
GO
IF SUSER_ID(N'indotel_api') IS NULL
    CREATE LOGIN [indotel_api] WITH PASSWORD = N'""" + literal + """', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN [indotel_api] WITH PASSWORD = N'""" + literal + """';
GO
USE [Indotel];
GO
IF USER_ID(N'indotel_api') IS NULL CREATE USER [indotel_api] FOR LOGIN [indotel_api];
IF IS_ROLEMEMBER(N'db_datareader', N'indotel_api') <> 1 ALTER ROLE [db_datareader] ADD MEMBER [indotel_api];
IF IS_ROLEMEMBER(N'db_datawriter', N'indotel_api') <> 1 ALTER ROLE [db_datawriter] ADD MEMBER [indotel_api];
GRANT EXECUTE TO [indotel_api];
GO
"""
    database = Path(__file__).resolve().parent.parent / "Database"
    scripts = [database / "Schema.sql", *sorted((database / "Migrations").glob("*.sql"))]
    return bootstrap + "\n".join(script.read_text(encoding="utf-8-sig") + "\nGO\n" for script in scripts)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    values = settings()
    sql = sql_configuration()
    if not values["DatabaseConnectionString"]:
        password = sql["SQL_APP_PASSWORD"].replace('"', '""')
        values["DatabaseConnectionString"] = (
            'Server=sqlserver,1433;Database=Indotel;User Id=indotel_api;Password="' + password +
            '";Encrypt=True;TrustServerCertificate=True;'
        )
    public = deployment()
    directory = Path(args.output)
    directory.mkdir(parents=True, exist_ok=True)
    runtime = {
        "services": {
            "api": {"environment": escape_dollars(values)},
            "redis": {"environment": escape_dollars({"REDIS_PASSWORD": values["Redis__Password"]})},
            "sqlserver": {"environment": escape_dollars({"MSSQL_SA_PASSWORD": sql["MSSQL_SA_PASSWORD"]})},
            "sql-init": {"environment": escape_dollars({"SQLCMDPASSWORD": sql["MSSQL_SA_PASSWORD"]})},
        }
    }
    path = directory / "runtime.compose.json"
    path.write_text(json.dumps(runtime, ensure_ascii=False), encoding="utf-8")
    path.chmod(0o600)
    bootstrap = directory / "sql-init.sql"
    bootstrap.write_text(sql_bootstrap(sql["SQL_APP_PASSWORD"]), encoding="utf-8")
    bootstrap.chmod(0o600)
    (directory / ".env").write_text("".join(key + "=" + value + "\n" for key, value in public.items()), encoding="utf-8")
    print("Rendered " + str(len(values)) + " API settings for Docker Compose.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError) as error:
        raise SystemExit(str(error))
