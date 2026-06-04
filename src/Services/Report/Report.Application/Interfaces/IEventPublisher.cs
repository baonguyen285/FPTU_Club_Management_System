using System.Threading.Tasks;

namespace Report.Application.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(string channel, T message);
    }
}
