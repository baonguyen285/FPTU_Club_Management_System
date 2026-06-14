using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Report.Application.Events;

namespace Report.Infrastructure.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync(ReportEvent evt);
    }

    public class RedisEventPublisher : IEventPublisher
    {
        private readonly IConnectionMultiplexer _mux;
        private const string Channel = "reports:events";

        public RedisEventPublisher(IConnectionMultiplexer mux)
        {
            _mux = mux;
        }

        public Task PublishAsync(ReportEvent evt)
        {
            var json = JsonSerializer.Serialize(evt);
            return _mux.GetSubscriber().PublishAsync(Channel, json);
        }
    }
}