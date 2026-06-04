using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Report.Application.Interfaces;

namespace Report.Infrastructure.EventBus
{
    public class RedisEventPublisher : IEventPublisher
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisEventPublisher(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task PublishAsync<T>(string channel, T message)
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(message);
            await db.PublishAsync(channel, json);
        }
    }
}
