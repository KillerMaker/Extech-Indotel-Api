# DigitalOcean deployment: API

This follows the payment API deployment pattern: GitHub Actions builds an immutable GHCR image and deploys it over SSH using Docker Compose and Caddy. This application uses a separate droplet from the payment API.

The API repository owns the .NET 10 API, SQL Server Express 2022, password-protected Redis and Caddy gateway. The frontend repository deploys an Nginx container on the same dedicated droplet. Caddy routes the two DNS names to `indotel-api:8080` and `indotel-portal:8080` over the shared Docker network. Host health ports bind only to 127.0.0.1.

## Droplet preparation

Use the dedicated Ubuntu droplet for this portal and API. Install Docker Engine, Docker Compose v2 (with `up --wait`), and curl. Give the deployment SSH user access to Docker and write access to the application directories. Create `/opt/exatech-indotel/api` and `/opt/exatech-indotel/portal`, owned by that user. Docker access grants administrative control of the host.

Point both DNS names to the droplet IPv4 address. Allow inbound SSH on your configured port, HTTP 80 and HTTPS 443; optionally allow UDP 443 for HTTP/3. Caddy in the API stack owns those web ports and obtains and renews certificates. Kafka, SQL Server Express 2022 and password-protected Redis run in this Compose stack with persistent volumes. The Rust email consumer is deployed separately and must connect to the shared Docker network.

Both stacks use the external Docker network `exatech-indotel`, subnet `172.29.20.0/24`. Deployment creates it automatically and rejects a conflicting subnet. Change DOCKER_NETWORK and DOCKER_SUBNET together in both repositories if this subnet overlaps your infrastructure. Use different hosts/networks/domains for Staging and Production; the default aliases are intended for one environment per droplet.

## GitHub configuration

Create GitHub Environments named `Staging` and `Production` in both repositories. Add the variables and secrets below to the corresponding Environment. Configure required reviewers on Production if you want release approval. Allow GitHub Actions to publish GHCR packages, and grant the deployment account read access to each private image package. `GHCR_TOKEN` is a token with `read:packages`; workflows publish with their automatic `GITHUB_TOKEN`. Add the public key matching `DROPLET_SSH_KEY` to the droplet user's authorized_keys.

| Setting | Type | Value |
| --- | --- | --- |
| DROPLET_HOST | Variable | Dedicated droplet IPv4 address or DNS hostname |
| DROPLET_USER | Variable | SSH user with Docker access |
| DROPLET_SSH_PORT | Variable | Optional, defaults to 22 |
| DROPLET_SSH_KNOWN_HOSTS | Variable | Recommended pinned OpenSSH known_hosts entries; if empty, ssh-keyscan discovers the key during deployment |
| DROPLET_SSH_KEY | Secret | Complete private SSH key, including header/footer |
| GHCR_USERNAME | Variable | Account owning GHCR_TOKEN; defaults to repository owner |
| GHCR_TOKEN | Secret | Token able to pull the private package |
| DOCKER_NETWORK | Variable | Optional, default exatech-indotel; same in both repos |
| DOCKER_SUBNET | Variable | Optional, default 172.29.20.0/24; same in both repos |

Use `.env.production.example` as the complete setting checklist. The workflows consume GitHub Environment values directly; they do not source that example file. Generated `.env` files contain public Compose settings; API secrets are rendered separately into `runtime.compose.json` with restrictive permissions. Never commit filled environment files or runtime bundles.

## API-specific deployment variables

| Variable | Value |
| --- | --- |
| PUBLIC_DOMAIN | API DNS hostname, for example api.indotel.example.com; no scheme or path |
| PORTAL_DOMAIN | Frontend DNS hostname, for example indotel.example.com; must equal the frontend PUBLIC_DOMAIN |
| DEPLOY_PATH | /opt/exatech-indotel/api; required absolute path |
| APP_PORT | Optional loopback health port; default 8080 |
| COMPOSE_PROJECT_NAME | Optional; default exatech-indotel-api; keep stable to retain volumes |

## Complete application configuration

`deployment/settings.schema.json` is the setting manifest used by `deployment/configure.py`. Set secret values under Environment Secrets, and other values under Environment Variables. Empty optional values use the defaults shown here. Locality overrides must be nonempty JSON string arrays. The renderer expands them into .NET indexed environment settings.

| GitHub setting | Type | .NET/runtime key | Requirement/default |
| --- | --- | --- | --- |
| ASPNETCORE_ENVIRONMENT | Variable | `ASPNETCORE_ENVIRONMENT` | Default: `Production` |
| DOTNET_ENVIRONMENT | Variable | `DOTNET_ENVIRONMENT` | Default: `Production` |
| API_ALLOWED_HOSTS | Variable | `AllowedHosts` | Default: `*` |
| JWT_SECRET_KEY | Secret | `SecretKey` | Required |
| SQL_CONNECTION_STRING | Secret | `DatabaseConnectionString` | Optional override; otherwise generated from SQL_APP_PASSWORD |
| WISPRO_CLIENTS_URL | Variable | `WisproClientsUrl` | Default: `https://www.cloud.wispro.co/api/v1/clients` |
| WISPRO_CONTRACTS_URL | Variable | `WisproContractsUrl` | Default: `https://www.cloud.wispro.co/api/v1/contracts` |
| WISPRO_API_KEY | Secret | `WisproApiKey` | Required |
| SIUBEN_PHASE1_URL | Variable | `Siuben__Phase1__Url` | Default: `https://servicios.siuben.gob.do:2043` |
| SIUBEN_PHASE1_USERNAME | Secret | `Siuben__Phase1__Username` | Required |
| SIUBEN_PHASE1_PASSWORD | Secret | `Siuben__Phase1__Password` | Required |
| SIUBEN_PHASE1_LOCALITIES | Variable | `Siuben__Phase1__Localities` | Default: `["Salinas", "Cristobal", "Batey 8", "Santana", "Cabeza de Toro", "Quita Coraza", "Fondo Negro", "Canoa", "Jaquimeyes", "Palo Alto", "Fundacion", "Pescaderia", "La Guazara", "Polo"]` |
| SIUBEN_PHASE1_PROVIDER | Variable | `Siuben__Phase1__Provider` | Default: `Exatech` |
| SIUBEN_PHASE1_DISABLED_CONTRACT_REASON | Variable | `Siuben__Phase1__DisabledContractReason` | Default: `CancelaciÃƒÂ³n` |
| SIUBEN_PHASE1_DISABLED_CONTRACT_OTHER_REASON | Variable | `Siuben__Phase1__DisabledContractOtherReason` | Optional; empty |
| SIUBEN_PHASE2_URL | Variable | `Siuben__Phase2__Url` | Required |
| SIUBEN_PHASE2_USERNAME | Secret | `Siuben__Phase2__Username` | Required |
| SIUBEN_PHASE2_PASSWORD | Secret | `Siuben__Phase2__Password` | Required |
| SIUBEN_PHASE2_LOCALITIES | Variable | `Siuben__Phase2__Localities` | Default: `["El Naranjo", "La lista", "Bombita", "El Memiso", "El higuito", "Las Minas", "Batey 7", "Batey 9", "Sabana Mula", "El Pon"]` |
| SIUBEN_PHASE2_PROVIDER | Variable | `Siuben__Phase2__Provider` | Default: `Exatech` |
| SIUBEN_PHASE2_DISABLED_CONTRACT_REASON | Variable | `Siuben__Phase2__DisabledContractReason` | Default: `CancelaciÃƒÂ³n` |
| SIUBEN_PHASE2_DISABLED_CONTRACT_OTHER_REASON | Variable | `Siuben__Phase2__DisabledContractOtherReason` | Optional; empty |
| REDIS_CONNECTION_STRING | Variable | `Redis__ConnectionString` | Default: `redis:6379,abortConnect=false`; keep credentials in `REDIS_PASSWORD` |
| REDIS_INSTANCE_NAME | Variable | `Redis__InstanceName` | Default: `exatech-indotel-api:` |
| EMAIL_KAFKA_BOOTSTRAP_SERVERS | Variable | `EmailQueue__BootstrapServers` | Default: `kafka:9092` |
| EMAIL_KAFKA_TOPIC | Variable | `EmailQueue__Topic` | Default: `email.requests` |
| EMAIL_SYSTEM | Variable | `EmailQueue__System` | Default: `exatech-indotel-api` |
| EMAIL_KAFKA_SECURITY_PROTOCOL | Variable | `EmailQueue__SecurityProtocol` | Default: `Plaintext` |
| EMAIL_KAFKA_SASL_MECHANISM | Variable | `EmailQueue__SaslMechanism` | Optional; empty |
| EMAIL_KAFKA_SASL_USERNAME | Secret | `EmailQueue__SaslUsername` | Optional; empty |
| EMAIL_KAFKA_SASL_PASSWORD | Secret | `EmailQueue__SaslPassword` | Optional; empty |
| EMAIL_RECEIVER | Variable | `EmailReciver` | Default: `internet@exatech.com.do` |
| CLIENT_CREATION_TEMPLATE | Variable | `ClientCreationTemplate` | Default: `ClientCreated` |
| CLIENT_UPDATE_REQUEST_TEMPLATE | Variable | `ClientUpdateRequestTemplate` | Default: `ClientUpdated` |
| USER_CREATION_TEMPLATE | Variable | `UserCreationTemplate` | Default: `UserCreated` |
| CORS_ALLOWED_ORIGINS | Variable | `Cors__AllowedOrigins` | Optional JSON array; defaults to https://PORTAL_DOMAIN |
| SWAGGER_ENABLED | Variable | `Swagger__Enabled` | Default: `false` |
| LOG_LEVEL_DEFAULT | Variable | `Logging__LogLevel__Default` | Default: `Information` |
| LOG_LEVEL_ASPNETCORE | Variable | `Logging__LogLevel__Microsoft.AspNetCore` | Default: `Warning` |
| SIUBEN_URL | Variable | `SiubenUrl` | Optional; empty |
| SIUBEN_USERNAME | Secret | `SiubenUsername` | Optional; empty |
| SIUBEN_PASSWORD | Secret | `SiubenPassword` | Optional; empty |
| REDIS_PASSWORD | Secret | `Redis__Password` | Required |
| TZ | Variable | `TZ` | Default: `America/Santo_Domingo` |
| DOCKER_SUBNET | Variable | `ReverseProxy__KnownNetworks__0` | Default: `172.29.20.0/24` |

The default Redis connection uses the Redis container. REDIS_PASSWORD is passed separately to both the API and Redis so passwords containing commas or dollar signs are preserved. If you choose an external Redis endpoint, REDIS_CONNECTION_STRING and REDIS_PASSWORD must match it; the bundled Redis container still runs. SQL_CONNECTION_STRING can override the bundled SQL Server connection. If it is empty or absent, deployment generates the internal connection from SQL_APP_PASSWORD. SQL bootstrap creates the Indotel database, indotel_api login, repository tables, default roles and email templates, then applies the incremental migrations. It does not create portal user accounts.

Email publishing starts as a discarded asynchronous task after the template is rendered; HTTP requests do not wait for Kafka delivery. Publication failures are caught and logged without failing the completed operation. There is no application queue, database storage, or application-level retry. Pending publication may be lost when the API restarts.

Deployment starts the bundled single-node Kafka broker before the API and waits for broker readiness. Set the API environment variable `EMAIL_KAFKA_BOOTSTRAP_SERVERS=kafka:9092` and `EMAIL_KAFKA_SECURITY_PROTOCOL=Plaintext`. Kafka has no published host ports and persists messages and broker metadata in `kafka_data`; normal deployments retain this volume. The broker uses a 512 MB JVM heap and seven-day log retention. This is a single broker without replication or high availability. The Rust email sender must join the same `exatech-indotel` Docker network and use `kafka:9092` to consume notifications; this pipeline deploys the broker, not the email sender.

Keep ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT equal (defaults Production, including on the Staging droplet). The deployed API disables Swagger by default, trusts forwarded headers from DOCKER_SUBNET, and allows only configured HTTPS CORS origins. JWT_SECRET_KEY must contain at least 32 UTF-8 bytes. EMAIL_KAFKA_SECURITY_PROTOCOL accepts Plaintext, Ssl, SaslPlaintext or SaslSsl; SASL also requires mechanism (Plain, ScramSha256 or ScramSha512), username and password. Kafka must be reachable by the API and by the Rust email sender.

## Deploy and verify

1. Configure DNS, the droplet and GitHub Environments in both repos.
2. Push to `phase2` or `develop` to test, build and deploy Staging. Pull requests test and build without deploying. For Production, run the workflow manually and choose Production.
3. Deploy the API first so Caddy and Redis are ready, then deploy the portal from its repository. Until the portal is deployed, its hostname can return 502.
4. Verify `https://PUBLIC_DOMAIN/health` and the portal login, client search and Wispro synchronization with your configured services.

The pipeline runs the Siuben checks, builds a .NET image, and checks container startup before deploying. The SSH deployment validates Compose, pulls the release, waits for container health, reloads Caddy and checks both loopback and public HTTPS health. `/health` reports process health; it does not verify SQL, Wispro, Siuben or Kafka connectivity. Failed public health checks fail the job; automatic rollback is not performed.

## API logs and OpenTelemetry

Client creation uses Wispro's returned IDs when available. When Wispro reports creation success without IDs, the API bypasses Redis and performs up to three cedula lookups within five seconds. Recovery accepts only one exact match (and must match any ID already returned by POST). It never repeats the creation POST. If identification remains unresolved, HTTP 409 with code `WISPRO_IDENTIFICATION_PENDING` explains that the client may already exist in Wispro and must not be created again; local SQL insertion has not occurred. This state is not stored as a background job and requires manual reconciliation if recovery fails. No new database tables are introduced.

Integration errors include a safe title, message, code, source, HTTP status and trace ID. Siuben custom business errors preserve `customTitle`, `customMessage` and `extraInformation`, without forwarding its internal exception fields. The portal displays the custom title and explanation/eligibility reason, includes a trace reference, and blocks resubmission of a creation whose Wispro identification is pending. Unexpected exceptions return a generic message rather than internal database/service details.

The API writes structured JSON console logs and correlates logs and spans by trace ID. Each response includes `X-Trace-Id`; error responses also include `traceId`. Search this ID in your observability backend to follow a request. Client creation emits named stages for duplicate checking, Wispro creation, SQL insertion and email preparation, plus a separate Kafka publish span. Each stage records elapsed milliseconds and failures. Incoming request spans, outgoing HttpClient spans, HTTP metrics and runtime meters are captured. Health requests are excluded from traces/request completion logs.

Remote export is optional: a blank endpoint keeps console logging active without sending data anywhere. Configure these GitHub Environment settings for an OTLP-compatible Collector or Elastic ingestion endpoint:

| Name | Kind | Value |
|---|---|---|
| `OTEL_SERVICE_NAME` | Variable | `exatech-indotel-api` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Variable | OTLP **base** endpoint, e.g. `http://otel-collector:4318` on the Docker network or your HTTPS Elastic Managed OTLP endpoint |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Variable | `http/protobuf` (default), or `grpc` (usually port 4317) |
| `OTEL_EXPORTER_OTLP_HEADERS` | Secret | Optional authentication headers, e.g. `Authorization=ApiKey%20YOUR_API_KEY` |

For HTTP/protobuf the API appends `/v1/logs`, `/v1/traces` and `/v1/metrics` to the base endpoint. Use an endpoint supporting all three signals. Exporters batch asynchronously and have a five-second export timeout; collector failure does not wait in the request path. Telemetry is best effort rather than durable storage. Service metadata includes the configured service name and ASP.NET environment.

Kibana is the UI, not the OTLP receiver: use Elastic's ingestion endpoint or a Collector forwarding to Elasticsearch, then view the service in Kibana. This change does not deploy Elasticsearch, Kibana or a Collector, nor create an Elastic subscription. An existing Collector must join the Docker network to use the example hostname. Keep tokens in the headers secret, not in the endpoint URL. Deployment tooling includes these settings in the generated API environment.

Instrumentation does not collect request/response bodies or authorization headers. Exported dependency URLs are reduced to their origin and query/path tags are removed so cedulas in URLs are not exported. Application request logs use route templates, not raw URLs. Error logs retain exception type and stack without exception text; detailed API error responses remain available to the caller. Keep arbitrary client data out of any future custom log messages. `deployment/check-telemetry.py` tests actual authenticated HTTP exports to a disposable local receiver, all three signals, trace headers and URL redaction; the pipeline runs this check before deployment.

On the droplet:

```bash
cd /opt/exatech-indotel/api
docker compose --env-file .env -f docker-compose.yml -f runtime.compose.json ps
docker compose --env-file .env -f docker-compose.yml -f runtime.compose.json logs --tail=100 api caddy redis
```

## Rollback and persistence

Images are tagged `<full-commit-sha>-staging` or `<full-commit-sha>-production`. To roll back, edit APP_IMAGE in the droplet `.env` to an earlier successful tag, then run `bash ./deploy.sh` from the deployment directory. Keep the runtime configuration compatible with that release. Redis and Caddy state use named volumes; preserve them and back up configuration securely. Avoid `docker compose down -v`, which deletes persisted data and certificate state. Subsequent pipeline deployments replace the runtime settings with the current GitHub Environment configuration.

## Upload all GitHub settings

Install GitHub CLI and authenticate with `gh auth login` using an account with administration access to this repository. Generate a private JSON template, fill the required null entries locally, then validate and apply it to the intended Environment:

```powershell
python deployment/github-settings.py --write-template .deployment-values.production.json
python deployment/github-settings.py --environment Production --values .deployment-values.production.json
python deployment/github-settings.py --environment Production --values .deployment-values.production.json --apply
```

Repeat with a separate values file for Staging. The script uses the same setting manifest as deployment, creates the GitHub Environment if needed, uploads credentials as encrypted Actions secrets and other settings as Actions variables. Values are sent through stdin and are never printed. Optional empty values are skipped, preserving any existing remote values. The generated private JSON files are gitignored; store and remove them according to your credential handling practices.

The API launch profile contains only the Development environment selector. For local development, provide application configuration with .NET user secrets or process environment variables using the .NET keys in the table. GitHub settings are injected by the deployment workflow; they are not automatically available to a local Visual Studio launch. The test and simulation projects intentionally retain fake credentials for isolated execution.

## Bundled SQL Server

The stack runs `mcr.microsoft.com/mssql/server:2022-latest` with Express edition, a persistent `sql_data` volume, and a configurable TCP port binding (loopback port 1433 by default). Use an x86-64 droplet with enough memory for SQL Server plus the API, Redis and portal; SQL Server needs at least 2 GB available to start. The configured SQL memory limit is 1536 MB; size the host with headroom. Express has edition capacity limits; choose an appropriately licensed edition if you outgrow it.

Add two API GitHub Environment secrets:

| Secret | Purpose |
| --- | --- |
| MSSQL_SA_PASSWORD | SQL Server administrator password |
| SQL_APP_PASSWORD | Password for the dedicated indotel_api login |

Use different passwords, 12-128 characters with at least three of uppercase, lowercase, digits and symbols. Neither value can be the placeholder `default`. Keep the administrator password stable after initial provisioning: changing its environment value does not rotate an existing persisted SQL Server administrator password. Rotate it through SQL Server before updating the matching secret. The bootstrap updates the application login password on deployment.

Leave SQL_CONNECTION_STRING unset/empty to generate:

```text
Server=sqlserver,1433;Database=Indotel;User Id=indotel_api;Password=<SQL_APP_PASSWORD>;Encrypt=True;TrustServerCertificate=True;
```

TrustServerCertificate is used for the bundled server's self-signed TLS certificate on the private Docker network. For an external SQL server with a trusted certificate, set an explicit SQL_CONNECTION_STRING with TrustServerCertificate=False. An existing secret named SQL_CONNECTION_STRING containing `default` must be replaced with a valid connection string or removed to use generation.

The init service waits for SQL health, creates the database and least-privilege application login (data reader/writer and execute; no schema administration), then allows API startup. The generated `sql-init.sql` contains the login password, is uploaded with restrictive permissions, and is gitignored. SQLCMD variable substitution is disabled and SQL literals are escaped. For a new database, deployment applies Database/Schema.sql and then Database/Migrations automatically. Existing tables/data and edited templates are preserved. The schema matches the Client, AppUser, UserRole and EmailTemplate repositories. A first administrator account must be provisioned separately; no default login/password is seeded. If importing an existing database, restore it before starting the API stack and verify compatibility with this schema.

Create regular SQL backups, copy them to storage outside the droplet, and test restores. A persistent volume protects data across container recreation, but is not a backup. No automatic backup schedule is included. Keep the Compose project name stable and do not remove the sql_data volume.

## Remote SQL access

Set these API GitHub Environment **variables** in the environment you deploy:

| Variable | Value for this droplet |
| --- | --- |
| `SQL_BIND_ADDRESS` | `134.209.217.173` |
| `SQL_PORT` | `1433` |

Before applying this public binding, attach/configure a DigitalOcean Cloud Firewall allowing inbound **TCP 1433 only from `152.166.124.80/32`**. Keep the existing SSH/HTTP/HTTPS rules. The Compose bind address selects the droplet interface, **not** the allowed client IP; the Cloud Firewall enforces the source restriction. These variables do not create a Cloud Firewall rule. Docker-published ports can bypass ordinary UFW rules, so use the DO Cloud Firewall for this allowlist. If your public source IP changes, update that rule.

Run Actions → API build and DigitalOcean deployment → Run workflow, select the configured environment, and check `recreate_database_container` **once** to apply the new port mapping to an existing SQL container. The same named `sql_data` volume is reattached; this does not clear the database. Subsequent deployments can leave the checkbox off. No Caddy or HTTP DNS routing is involved for SQL Server.

In SSMS or another SQL client use server `134.209.217.173,1433`, database `Indotel`, SQL authentication, and the existing credentials. For application-level access:

```text
Server=134.209.217.173,1433;Database=Indotel;User Id=indotel_api;Password=<SQL_APP_PASSWORD>;Encrypt=True;TrustServerCertificate=True;
```

Use `sa` with `MSSQL_SA_PASSWORD` only when administrative access is needed. The bundled SQL Server currently uses a self-signed TLS certificate, hence the explicit trust setting above; use a trusted server certificate and certificate validation when provisioned. The API keeps its internal `sqlserver,1433` connection string. Reset `SQL_BIND_ADDRESS` to `127.0.0.1` and recreate the container to remove direct public access.

## Database retention during releases

Normal push deployments and manual releases default RECREATE_DATABASE_CONTAINER to false. The deploy script uses `up --no-recreate` for SQL Server: an existing container is retained and restarted if needed; an absent container is created on the first deployment. API/Caddy updates are deployed separately, and sql-init runs additive schema/migration scripts against the retained database. Redis is also retained by default.

For an intentional SQL container/image refresh, run the workflow manually and select `recreate_database_container`. This uses `--force-recreate` for SQL Server while retaining the existing sql_data volume and records. It is not a reset flag and never deletes the database volume. Back up the database before intentional maintenance.

The `.sql-data-volume` marker in the droplet deployment directory records the initial volume name. Later deployments abort if that volume is missing or the Compose project name selects another volume, rather than silently initializing an empty database. Keep DEPLOY_PATH, COMPOSE_PROJECT_NAME and the marker stable. New Staging/Production environments need separate deployment directories/project names unless they intentionally share this same installation. A fresh directory cannot detect an old installation automatically.

The pipeline never runs `down -v` or removes production volumes. Future feature migrations still require review and backups: container retention does not make destructive SQL statements safe. The CI persistence test verifies both normal container retention and record survival after explicit container recreation.
