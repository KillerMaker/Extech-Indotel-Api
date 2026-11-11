# Siuben phases

The API chooses a Siuben integration from the supplied `locality`. Locality is
required for client checks and creation. Matching ignores case, accents, and
extra whitespace. Unknown or missing localities return HTTP 400; there is no
fallback to the other phase.

Locality lists are configured in `appsettings.json` under
`Siuben:Phase1:Localities` and `Siuben:Phase2:Localities`. Edit those arrays to
add, remove, or reassign localities, then restart the API. No code change is
required. Both phases must have a non-empty list. Startup validation rejects
blank names and duplicate names, including names that match after case, accent,
or whitespace normalization, within a phase or across phases.

| Phase | Localities |
| --- | --- |
| 1 | Salinas, Cristobal, Batey 8, Santana, Cabeza de Toro, Quita Coraza, Fondo Negro, Canoa, Jaquimeyes, Palo Alto, Fundacion, Pescaderia, La Guazara, Polo |
| 2 | El Naranjo, La lista, Bombita, El Memiso, El higuito, Las Minas, Batey 7, Batey 9, Sabana Mula, El Pon |

## Configuration and deployment

Phase 1 retains support for the existing `SiubenUrl`, `SiubenUsername`, and
`SiubenPassword` settings. Alternatively, configure `Siuben__Phase1__Url`,
`Siuben__Phase1__Username`, and `Siuben__Phase1__Password`; these take precedence
over the legacy settings. Configure phase 2 separately using:

```text
Siuben__Phase2__Url=https://your-siuben-host
Siuben__Phase2__Username=<phase 2 user>
Siuben__Phase2__Password=<phase 2 password>
Siuben__Phase2__Provider=Exatech
```

Each phase requires its own credentials supplied by Siuben, even if the URL is
the same. Tokens are cached separately for one hour with absolute expiration. The first
request after expiration obtains a new token; concurrent requests across API
instances share the same login or refresh using Redis. See `REDIS.md` for setup.
A 401 refreshes the token
and retries the operation once. Phase 2 never inherits phase 1 credentials.
Configuration is checked when the selected phase is first resolved.

Apply `Database/Migrations/20261103_AddClientLocality.sql` before deploying the
API. New clients store the canonical locality name. Existing rows keep NULL;
backfill only from verified locality data. The API does not infer a locality
from `city`, and does not classify historical clients automatically.

Update callers to send `locality` when checking and creating clients.

## Client check

The portal loads the configured locality names and phases from authenticated
`GET /api/siuben/localities`. Its response is an array such as
`[{"name":"Salinas","phase":1},{"name":"Bombita","phase":2}]`.
Deploy this endpoint with the phase 2 portal so it can populate the locality selector.

```http
GET /api/Client/check?documentNumber=00100118793&locality=Bombita
Authorization: Bearer <portal token>
```

Phase 1 calls `/api/Data/get/{cedula}` and preserves the existing check fields
and poverty behavior. Phase 2 calls `/api/data_fase2/get/{cedula}` and returns:

```json
{
  "existsInWispro": false,
  "existsInSiuben": true,
  "clientId": null,
  "contractNumber": null,
  "pobertyLevel": null,
  "siubenPhase": 2,
  "isEligible": true,
  "reason": "Elegible"
}
```

Phase 2 supplies no poverty level or contract number; these remain null in the
check response. Siuben 404 returns `existsInSiuben: false`. Phase 2 business
rejections return HTTP 400 with Siuben's `extraInformation` as `message`.
Upstream authentication/authorization and server failures return HTTP 502.
For phase 1, `isEligible` and `reason` are null rather than an inferred eligibility.

## Client creation

Include `"locality": "Bombita"` in the existing `POST /api/Client/create` body.
Locality is saved and returned in the creation response. Phase 1 still requires
`pobertyLevel`. Phase 2 allows it to be omitted (the existing integer storage
uses 0 for an unspecified value).

Client creation saves the client and sends its creation email. It does not look
up a Wispro contract or call `HabilitarBeneficiado`, and its response has no
`siubenActivated` field.

## Scheduled contract synchronization and activation

The existing scheduled job performs phase 2 activation. Apply
`Database/Migrations/20261103_AddClientSiubenActivation.sql` before deploying it.
This adds a persistent `SiubenActivated` flag so completed activations are
skipped. For clients already activated before this migration, set the flag to 1
only after verifying their status with Siuben.

The job looks up each pending client's existing Wispro contract by document
number and verifies the contract's client ID. It uses that contract's `public_id`
for `numeroContrato` and `created_at` for `fechaContrato`. The provider comes
from `Siuben:Phase2:Provider` (default `Exatech`). The client's mobile phone is
used, falling back to the fixed phone when mobile is empty. Common phone
formatting and a leading country code `1` are removed; 10 digits are required.

The job checks all saved clients on each run, including clients whose Siuben
activation has already completed. For saved contract numbers it also applies
Wispro's `public_id_eq` filter and verifies the returned contract number and
client ID. Contract lookup has no date window, so older contracts are included.
Ambiguous contracts are rejected.

For phase 2, a Wispro contract with state `enabled` activates the beneficiary if
not already active. After Siuben confirms activation, the job persists the
contract number, `SiubenActivated = 1`, and `SiubenDisabled = 0`.

For state `disabled`, the job calls `/api/data_fase2/DeshabilitarBeneficiado`
using the client's normalized document number and reason `Cancelación`. This
reason is configurable with `Siuben:Phase2:DisabledContractReason`. If changed
to `Otro`, also configure `Siuben:Phase2:DisabledContractOtherReason`.

Apply `Database/Migrations/20261103_AddClientSiubenDisabled.sql` before deploying
this state synchronization. Successful deactivation persists
`SiubenDisabled = 1` and `SiubenActivated = 0`. Later runs still check the contract
but do not repeat a confirmed deactivation. Siuben's specific HTTP 400 response
`Hogar ya está de baja` also confirms the desired disabled state; other errors
are not treated as success. If Wispro later returns to `enabled`, the job can
activate the beneficiary again and clear the disabled flag.

States such as `alerted`, `degraded`, or an absent state do not trigger either
Siuben operation. A missing contract or failed Siuben call is retried on a later
run. Failures are logged per client and do not prevent the remaining clients
from being processed.

Phase 1 clients continue to have their contract numbers synchronized without
calling the phase 2 activation endpoint. Historical clients without a known
locality also receive contract synchronization but are not assigned a phase or
activated automatically. The configured schedule is unchanged, and Quartz
prevents overlapping executions of this job within the same scheduler.

The Siuben call and database update are separate operations. A process failure
between them can result in another synchronization attempt on a later run.

The Wispro lookup uses its documented
[`client_national_identification_number_eq` contract filter](https://doc.cloud.wispro.co/reference/contracts).

## Synchronize an existing client's benefit with Wispro

`GET /api/client?documentNumber=00100118793` requires authentication and returns
`{ client, wisproContract, alreadySynchronized, canSynchronize, synchronizationBlockedReason }`.
`client` contains all fields from the local repository; `wisproContract` is fetched
from Wispro by the stored Wispro ID and contract number. A missing local client
returns 404. A client without a contract still returns its data with
`wisproContract: null`, `canSynchronize: false`, and an explanatory message.

The portal searches first, shows the client and live Wispro contract as read-only,
and offers only **Sincronizar con Wispro** to admins. It does not allow selecting
an enable/disable action. Cancellation reasons remain editable when the contract
is disabled.

```http
PUT /api/siuben/synchronize
Content-Type: application/json
Authorization: Bearer <Admin portal token>

{
  "cedula": "00100118793",
  "motivoCancelacion": "Otro",
  "otroMotivoCancelacion": "Mudanza fuera de la zona"
}
```

The API fetches the current contract again at synchronization time. It derives the
locality from the stored client. Only configured phase 2 clients are supported.
An `enabled` Wispro contract calls Siuben's `HabilitarBeneficiado` using the stored
phone, configured provider, and Wispro's contract number and creation date.
A `disabled` contract calls `DeshabilitarBeneficiado` using the supplied reason
(default `Cancelación`). Valid reasons are `Cancelación`, `Falta de pago`,
`Error de inclusión`, and `Otro`; `Otro` requires a description (max 100 characters).
Missing contracts or any other state return 409 without a Siuben call or local
state update. Missing clients return 404. Sellers receive 403; anonymous users 401.

After Siuben succeeds, the API updates local flags and contract number and returns
`{ razon, clientDetails }`, with the current contract and updated client data.
It does not change Wispro. The old `/enable`, `/enable-stored`, and `/disable`
portal endpoints have been removed. Automated scheduled synchronization continues
to run independently.

## Verification

```powershell
dotnet run --project Tests/SiubenChecks/SiubenChecks.csproj
dotnet build --configuration Release
```

The checks use mocked HTTP responses and do not contact Siuben, change subsidies,
send mail, or write to the database. Live verification requires both sets of
credentials and an authorized test beneficiary.

When Wispro is enabled and the saved client is activated (or Wispro is disabled
and the saved client is disabled), `alreadySynchronized` is true and
`canSynchronize` is false. Unknown or contradictory local flags do not count as
synchronized. The portal shows "Ya está sincronizado con Wispro" without an action.
The synchronize endpoint rechecks this condition and returns 409 with current
`clientDetails`, without calling Siuben or updating the repository. A subsequent
Wispro state change allows synchronization again.
