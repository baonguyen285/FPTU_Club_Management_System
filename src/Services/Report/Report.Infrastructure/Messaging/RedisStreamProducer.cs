using StackExchange.Redis;

namespace Report.Infrastructure.Messaging;

public sealed class RedisStreamProducer : IRedisStreamProducer
{
    private readonly IConnectionMultiplexer _redis;
    public RedisStreamProducer(IConnectionMultiplexer redis) => _redis = redis;

    public async Task AddAsync(string stream, string field, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().StreamAddAsync(stream, field, value);
    }
}
