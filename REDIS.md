# Redis cache

The API uses Redis for all application caching through `ICacheService`:

| Data | Absolute lifetime |
| --- | --- |
| Siuben authentication tokens, separately per phase/URL/user | 1 hour |
| Wispro client lookups | 15 minutes |
| Email templates | 12 hours |

Configure `Redis:ConnectionString` and `Redis:InstanceName` in `appsettings.json`
or via `Redis__ConnectionString` and `Redis__InstanceName` environment variables.
All API instances that should share cached data must use the same Redis server
and instance name. Use a distinct instance name for each environment. Connection
strings use StackExchange.Redis syntax, including authentication and TLS options
when required by the Redis deployment.

## Local development

Start Docker Desktop, then run:

```powershell
docker compose -f compose.redis.yaml up -d
```

The default API connection is `localhost:6379`. This Compose service binds to the
local machine only. If the API runs in the same Compose network, use `redis:6379`.
The Redis data volume retains cache entries and their expiry across restarts.

```powershell
docker compose -f compose.redis.yaml down
```

## Token refresh

Siuben tokens are shared across API instances and reused until their one-hour
absolute expiration. A 401 causes one refresh/retry; requests that rejected an
older token reuse a newer token already published by another instance.

Refreshes use a per-key Redis lock with a 30-second lease and an ownership-checked
Lua write. Login is limited to 20 seconds. If the lease expires before publishing,
the old owner cannot overwrite another instance's token. Waiting for another
refresher is limited to 40 seconds. Token and lock keys use matching Redis Cluster
hash tags. Keys are hashed and namespaced; cache values are JSON.

The API shares one Redis connection multiplexer. Redis failures are propagated;
there is no in-memory fallback or uncached authentication fallback. Redis must
be available for cache-dependent operations. This refresh coordination is a cache
stampede guard, not an exactly-once guarantee across Redis failover.

## Verification

```powershell
dotnet run --project Tests/SiubenChecks/SiubenChecks.csproj
```

The standard checks use cache and Redis command doubles. To additionally test a
running Redis server:

```powershell
$env:REDIS_TEST_CONNECTION = 'localhost:6379'
dotnet run --project Tests/SiubenChecks/SiubenChecks.csproj
```

Live checks use a unique temporary key namespace, verify cross-instance reads,
expiration, and refresh locking, and leave only short-lived entries. They do not
contact Siuben or send email.
