using System.Reflection;
using StackExchange.Redis;

public class RedisDatabaseStub : DispatchProxy
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (RedisValue Value, DateTimeOffset Expires)> _entries = new();
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, TimeSpan> Lifetimes { get; } = new();
    public bool LoseNextLock { get; set; }
    public bool FailReads { get; set; }

    private RedisValue Get(string key) => _entries.TryGetValue(key, out var entry) && entry.Expires > Now
        ? entry.Value : RedisValue.Null;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        lock (_gate)
        {
            var arguments = args!;
            switch (targetMethod!.Name)
            {
                case "StringGetAsync":
                    return FailReads ? Task.FromException<RedisValue>(new InvalidOperationException("Redis unavailable"))
                        : Task.FromResult(Get(arguments[0]!.ToString()!));
                case "StringSetAsync":
                {
                    var key = arguments[0]!.ToString()!;
                    var ttl = (TimeSpan)arguments[2]!;
                    _entries[key] = ((RedisValue)arguments[1]!, Now + ttl);
                    Lifetimes[key] = ttl;
                    return Task.FromResult(true);
                }
                case "LockTakeAsync":
                {
                    var key = arguments[0]!.ToString()!;
                    if (!Get(key).IsNull) return Task.FromResult(false);
                    _entries[key] = ((RedisValue)arguments[1]!, Now + (TimeSpan)arguments[2]!);
                    return Task.FromResult(true);
                }
                case "LockReleaseAsync":
                {
                    var key = arguments[0]!.ToString()!;
                    if (Get(key) != (RedisValue)arguments[1]!) return Task.FromResult(false);
                    _entries.Remove(key);
                    return Task.FromResult(true);
                }
                case "ScriptEvaluateAsync":
                {
                    var keys = (RedisKey[])arguments[1]!;
                    var values = (RedisValue[])arguments[2]!;
                    if (LoseNextLock)
                    {
                        LoseNextLock = false;
                        _entries[keys[0].ToString()] = ("another-owner", Now + TimeSpan.FromSeconds(30));
                    }
                    if (Get(keys[0].ToString()) != values[0])
                        return Task.FromResult(RedisResult.Create((RedisValue)0));
                    var ttl = TimeSpan.FromMilliseconds((long)values[2]);
                    _entries[keys[1].ToString()] = (values[1], Now + ttl);
                    Lifetimes[keys[1].ToString()] = ttl;
                    return Task.FromResult(RedisResult.Create((RedisValue)1));
                }
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}
