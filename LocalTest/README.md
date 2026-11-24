# Local test with Wispro and Siuben phase 2

This runs the actual API against the external test services you provided and an isolated local SQL Server. It differs from `Simulation/`, which mocks integrations and storage.

| Component | Address |
| --- | --- |
| API / Swagger | `http://localhost:5288/swagger` |
| SQL Server | `127.0.0.1,11433`, database `Indotel` |
| Wispro clients | `https://www.cloud.wispro.co/api/v1/clients` |
| Wispro contracts | `https://www.cloud.wispro.co/api/v1/contracts` |
| Siuben phase 2 | `https://testindotelapi.siuben.gob.do/INDOTELAPI` |
| Local Kafka | `localhost:12992`, topic `email.requests.local-test` |
| Optional portal | `http://127.0.0.1:4301` |

Use your **Wispro test-account API key**: Wispro uses the same cloud hostname for different accounts, so the credential determines which account receives changes. Requests to create clients or synchronize beneficiaries really modify those external test services. Local SQL starts empty except for roles, templates and a local administrator. No production database is connected or copied. Phase 1 is deliberately pointed at a closed local endpoint. Use a phase 2 locality such as `El Naranjo`.

## Requirements

- Docker Desktop running with Linux containers, at least 6 GB of available Docker memory, and internet access to pull images/reach the test services.
- Python 3 (standard library only). The API builds in Docker with .NET 10.
- Your test Wispro API key and Siuben phase 2 username/password. Allow your outbound IP at either service if required.

## Configure and start

From the API repository root:

```powershell
python LocalTest/run.py init
```

Open `LocalTest/settings.local.json` and fill only these three fields initially:

```json
{
  "WisproApiKey": "YOUR_TEST_API_KEY",
  "SiubenPhase2Username": "YOUR_TEST_USERNAME",
  "SiubenPhase2Password": "YOUR_TEST_PASSWORD"
}
```

Keep the other generated settings. The file already includes both test URLs and generated SQL, Redis, JWT and portal credentials. It is ignored by Git and excluded from Docker builds. Don't paste credentials into chat or commit them. On Windows keep the file in your private user checkout; POSIX permissions are restricted where supported.

```powershell
python LocalTest/run.py configure
python LocalTest/run.py up
```

Startup retains existing infrastructure containers, creates the database/schema and email topic if missing, then builds and starts the API. The SQL login has the same application permissions as deployment. Database, Redis and Kafka volumes persist across restarts. Configuration errors do not call either external API.

The local admin email is `localtest@example.test`; its password is the generated `LocalAdminPassword` in your settings file. The seed inserts the account only when absent: changing that setting afterward does not reset an existing account's password. Database administrator password changes likewise require an explicit SQL rotation; don't regenerate the settings file for an existing database.

Swagger login: `POST /api/Authorization/login` with `email` and `password`. Copy the returned token into Swagger's **Authorize** dialog.

## Test the integrations

Check external authentication and read access without requiring an existing test client:

```powershell
python LocalTest/run.py probe
```

This logs into Siuben phase 2 and queries both services using the synthetic `00000000000` document. A Siuben 404 or its specific 403 `Cédula inválida` response is expected for that value; other 403 responses still fail. This verifies connectivity and authentication, not eligibility for a valid document. No client/beneficiary writes occur, and credentials, tokens and returned records are not printed.

First verify local health and database-backed login:

```powershell
python LocalTest/run.py smoke
```

Then use a cedula provided for your external test accounts:

```powershell
python LocalTest/run.py smoke --document YOUR_11_DIGIT_TEST_CEDULA --locality "El Naranjo"
```

That second command calls the real API's `GET /api/Client/check`, which looks up the document in both Wispro and Siuben phase 2. It prints only existence/eligibility flags, without displaying tokens or customer data. It does not create a client or change beneficiary status. Successful transport can return `false` if the supplied test document doesn't exist.

To test the full flow in Swagger or the portal:

1. Check an eligible test document and create a client. This creates the client in Wispro's test account and in local SQL.
2. Create its contract in the Wispro test account; this API doesn't create Wispro contracts.
3. Find the client in contract management and synchronize as the local administrator. Wispro's current contract state determines whether Siuben phase 2 is enabled or disabled. The API blocks synchronization when there is no contract or when local status already matches.
4. Change the contract state in Wispro test and synchronize again to exercise the other action. Cancellation can use `Cancelación`.

Wispro can remove a contract instead of marking it disabled. If the client has a previously recorded contract number and a successful lookup confirms no contracts remain, synchronization cancels the Siuben benefit and retains the old number for history. A client who never had a recorded contract stays blocked. The job uses the same rule, skips already cancelled beneficiaries and retries failed cancellations. An active replacement contract is used instead; ambiguous results, malformed responses and connection failures do not confirm deletion.

Scheduled synchronization is **disabled by default** (`ScheduledJobEnabled: false`) so external changes occur when you explicitly test an action. Set it to `true` and run `up` again to use the existing daily 23:59 schedule; there is no immediate scheduled trigger in this setup.

Email requests go to the isolated local Kafka topic. No email sender or SMTP service is started, so notifications are queued without being delivered.

## Optional frontend

In a second terminal, from the API root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\LocalTest\Start-Frontend.ps1 -FrontendPath "C:\path\to\Exatech-Indotel-Portal"
```

This uses the portal's existing relative `/api` build configuration with a generated proxy pointing to the real local API; it does not start the simulation host. No frontend files are modified. Install the frontend's npm dependencies first. Stop it with Ctrl+C.

## Status and stop

```powershell
python LocalTest/run.py status
python LocalTest/run.py logs
python LocalTest/run.py down
```

`down` stops/removes only this Compose project's containers and network; it keeps database/Kafka/Redis volumes. No command in this launcher deletes volumes. If you change ports, run `down` before `up` because infrastructure containers are normally retained. Production deployment files and simulation settings are separate.

Connect a SQL client with `Server=127.0.0.1,11433;Database=Indotel;User Id=sa;Password="<SqlAdminPassword from the local settings>";Encrypt=True;TrustServerCertificate=True;`. The self-signed certificate exception is local only. Use `indotel_api` and `SqlAppPassword` to inspect the application's database permissions.
