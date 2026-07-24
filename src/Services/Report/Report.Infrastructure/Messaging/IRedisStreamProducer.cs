namespace Report.Infrastructure.Messaging;

public interface IRedisStreamProducer
{
    Task AddAsync(string stream, string field, string value, CancellationToken cancellationToken = default);
}
