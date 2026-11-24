# Importing the legacy Azure SQL database

This is a one-time operator tool, separate from the API and deployment pipeline. It copies `UserRole`, `AppUser`, `Client`, and `EmailTemplate` directly between SQL servers, without exporting passwords or customer data to disk. It never writes to the source.

Install the .NET 10 SDK. Initialize the destination using `Database/Schema.sql` and the incremental migrations first. Use a destination administrator account capable of bulk inserts, preserving identity values and reseeding identities. The destination must have **no users or clients**; the default roles and email templates are allowed. Back up the destination and stop the API/scheduled jobs during the migration and verification. Source reads use a serializable transaction and can briefly block writers; pause legacy writes for the final cutover so later changes are not missed. The source tables are loaded into memory, so run on a machine with enough RAM.

## Configure connections

In PowerShell, set the following environment variables. Obtain the target connection string from your deployment credentials; do not commit it or paste it into chat. The tool does not read the application's `SQL_CONNECTION_STRING` implicitly.

```powershell
$env:MIGRATION_SOURCE_CONNECTION_STRING = 'Server=exatech-rd-db-server.database.windows.net,1433;Database=exatech-rd-db;Encrypt=True;TrustServerCertificate=False;'
$env:MIGRATION_SOURCE_ENTRA_TENANT_ID = 'b911035c-c91a-48b3-a3fe-db2743e30dd3'
$secureConnection = Read-Host 'Destination SQL connection string' -AsSecureString
$env:MIGRATION_TARGET_CONNECTION_STRING = [System.Net.NetworkCredential]::new('', $secureConnection).Password
```

The source opens a Microsoft Entra browser sign-in. Your account needs access to the Azure SQL database and your IP must be allowed by its firewall. For SQL-password or another SqlClient authentication mode instead, leave `MIGRATION_SOURCE_ENTRA_TENANT_ID` unset and put the authentication settings in the source connection string.

The DO database is private to Docker. Reach it through an SSH tunnel to its private container IP, rather than exposing port 1433 publicly. On the droplet, get the current address with:

```sh
docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$(docker compose -p exatech-indotel-api ps -q sqlserver)"
```

Run that command from the deployment directory with its Compose configuration, or pass the SQL container name directly to `docker inspect`. From your local machine, keep this tunnel open (replace `CONTAINER_IP`):

```powershell
ssh -N -L 11433:CONTAINER_IP:1433 root@134.209.217.173
```

The target connection string then uses `Server=127.0.0.1,11433;Database=Indotel;User Id=sa;Password="YOUR_PASSWORD";Encrypt=True;TrustServerCertificate=True;`. The certificate exception is for the bundled SQL Server self-signed certificate through the SSH tunnel. Use normal certificate validation for other SQL servers.

## Validate, then import

Run from the API repository root:

```powershell
dotnet run --project Database/LegacyMigration
dotnet run --project Database/LegacyMigration -- --apply
```

The first command performs preflight reads only. The second repeats preflight, then writes in one transaction. Any import failure rolls back all imported rows and template updates. Target SQL collation and constraints are checked during the actual insert; preflight does not guarantee those checks will pass. Logs show counts and field names, not customer records or passwords. A successful import cannot be applied again because the destination is no longer empty. The tool does not delete, truncate or merge users/clients.

Migration rules:

- Preserve user/client IDs, creator relationships, Wispro/public IDs, contract numbers and password hashes. Reseed identity counters for subsequent API inserts.
- Preserve role IDs. Stop if an existing target role ID has a different name. Unrecognized role names retain their names and gain no new API permissions. Role-name uniqueness is enforced by the target.
- Reject missing public IDs, identification numbers, user roles or other required values, broken relationships, duplicate normalized identification numbers, duplicate Wispro IDs/emails/roles and duplicate template names. No fabricated IDs or automatic privilege assignments.
- Convert null client `Street` and `Number` values to empty strings. Other invalid required fields need a reviewed correction before retrying.
- Preserve legacy email templates by replacing same-name destination templates; destination-only seed templates remain.
- Legacy clients get `Locality = NULL`, `SiubenActivated = false`, and `SiubenDisabled = false` from the target schema. Historical Siuben status cannot be inferred from a contract number. Assign localities and reconcile actual beneficiary status before enabling synchronization jobs to avoid sending incorrect or repeated Siuben requests.

Verify counts, representative client lookups, role permissions and portal logins before switching traffic. Keep the old database available until cutover is confirmed. To undo a committed import, restore the destination backup; this tool does not provide destructive rollback commands.

Remove credentials from this PowerShell session when finished:

```powershell
Remove-Item Env:MIGRATION_TARGET_CONNECTION_STRING
Remove-Variable secureConnection
```

## Local checks

```powershell
dotnet run --project Database/LegacyMigration -- --self-test
```

These exercise preflight acceptance, null values, orphan relationships, duplicate templates and duplicate masked/unmasked identification numbers without connecting to a database. They do not replace a rehearsal against a disposable destination before production cutover.
