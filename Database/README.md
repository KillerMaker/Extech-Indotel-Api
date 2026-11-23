# Indotel database

The deployment bootstrap creates the Indotel database and dedicated indotel_api login, runs Schema.sql, and applies Migrations/*.sql in filename order. The sql-init service must complete successfully before API startup. No separate manual schema import is required for a new bundled database.

Schema.sql creates the repository tables only when absent:

- UserRole: fixed IDs 1 (Admin) and 2 (Seller), role name and description.
- AppUser: identity UserId, unique email, password hash, name, phone, and role foreign key.
- Client: all Client entity fields, identity ClientId, unique WisproId and normalized document number, creator foreign key, contract and Siuben status fields.
- EmailTemplate: unique template name and HTML body.

The legacy column spelling PobertyLevel is intentional: the Client repository and entity use it. The schema includes the phase/locality and activation/deactivation fields introduced by the incremental migrations. UserRepository consistently uses dbo.AppUser / dbo.UserRole and maps their UserId / RoleId columns.

The default roles and ClientCreated, ClientUpdated and UserCreated email templates are inserted only when missing. Existing tables, users, client records and edited templates are preserved on repeat deployments. Schema.sql is not a general repair/migration engine for unrelated legacy schemas; existing databases must already have the base repository columns, with supported upgrades in Migrations.

## First administrator

No default portal account is created. Provision the first account through an administrator database connection by inserting into dbo.AppUser with RoleId = 1. Its Password value must be the output of the API's Sha256Hash extension (SHA-256 over UTF-8, in the exact hex format used by that method), not plaintext. Subsequent accounts can be created through the admin-only API endpoint. The indotel_api SQL login is a database service account, not a portal user.

## Applying to an external server

Create/select the target database as a SQL administrator, execute Schema.sql with sqlcmd -b -x, then execute the incremental migration files in order. The -x option preserves the $$...$$ email template tokens by disabling sqlcmd variable substitution. Supply authentication through SQLCMDPASSWORD or your normal secure connection method. Point SQL_CONNECTION_STRING to the initialized database afterward.

## Validation

CI runs deployment/check-sql.py against disposable SQL Server containers. It bootstraps twice to verify repeat initialization and executes Tests/SqlChecks against the actual repositories with the non-administrator API login. Test data and volumes are isolated and removed afterward.
