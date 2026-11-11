namespace Exatech_Indotel_API.Services.Cache;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan lifetime) where T : class;
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> create,
        Func<T, bool>? isValid = null) where T : class;
}
