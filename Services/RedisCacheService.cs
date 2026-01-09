using Microsoft.Extensions.Caching.Distributed;
using MarkdownGenQAs.Interfaces;

namespace MarkdownGenQAs.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IJsonService _jsonService;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        IJsonService jsonService,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _jsonService = jsonService;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cachedData = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(cachedData))
            {
                return default;
            }

            return _jsonService.Deserialize<T>(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key {Key} from Redis", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var serializedData = _jsonService.Serialize(value!);
            var options = new DistributedCacheEntryOptions();

            if (ttl.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = ttl;
            }

            await _cache.SetStringAsync(key, serializedData, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} in Redis", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing key {Key} from Redis", key);
        }
    }
}
