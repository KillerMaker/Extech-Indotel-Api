using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Exatech_Indotel_API.Services.Cache;

public class RedisCacheService(IDatabase database, IOptions<RedisOptions> options) : ICacheService
{
    private static readonly TimeSpan LockLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(40);
    private const string PublishScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('SET', KEYS[2], ARGV[2], 'PX', ARGV[3])
            return 1
        end
        return 0
        """;

    private RedisKey Key(string key)
    {
        // Hash tags keep the value and lock in the same Redis Cluster slot.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return $"{options.Value.InstanceName}{{{hash}}}";
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await database.StringGetAsync(Key(key));

        return value.IsNull ? null : JsonSerializer.Deserialize<T>((string)value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan lifetime) where T : class
    {
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));

        await database.StringSetAsync(Key(key), JsonSerializer.Serialize(value), expiry: lifetime, keepTtl: false);
    }

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> create,
        Func<T, bool>? isValid = null) where T : class
    {
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));

        var cacheKey = Key(key);
        RedisKey lockKey = $"{cacheKey}:lock";
        RedisValue owner = Guid.NewGuid().ToString("N");
        var waiting = Stopwatch.StartNew();

        while (waiting.Elapsed < LockWaitTimeout)
        {
            var cached = await GetAsync<T>(key);

            if (cached is not null && (isValid is null || isValid(cached))) return cached;

            if (await database.LockTakeAsync(lockKey, owner, LockLifetime))
            {
                try
                {
                    cached = await GetAsync<T>(key);

                    if (cached is not null && (isValid is null || isValid(cached))) return cached;

                    var value = await create();
                    var published = await database.ScriptEvaluateAsync(PublishScript,
                        [lockKey, cacheKey], [owner, JsonSerializer.Serialize(value), (long)lifetime.TotalMilliseconds]);

                    if ((long)published != 1)
                        throw new InvalidOperationException("Redis refresh lock expired before the cache entry could be published.");

                    return value;
                }
                finally
                {
                    await database.LockReleaseAsync(lockKey, owner);
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Timed out waiting for a Redis cache refresh.");
    }
}
