using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.EventBus;
using Notification.Infrastructure.GrpcClients;
using Notification.Infrastructure.Hubs;
using Notification.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Shared.Kernel.IntegrationEvents;
using StackExchange.Redis;
using Xunit;

namespace CommunicationReliability.Tests;

public sealed class RedisConsumerTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis = new();
    private readonly Mock<IDatabase> _mockDb = new();
    private readonly Mock<IIdentityDirectoryClient> _mockIdentityClient = new();
    private readonly Mock<ISingleClientProxy> _mockClientProxy = new();
    private readonly Mock<ILogger<RedisStreamsConsumer>> _mockLogger = new();
    private readonly NotificationDbContext _dbContext;

    private class FailingNotificationDbContext : NotificationDbContext
    {
        public FailingNotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("DB concurrency/constraint error");
        }
    }

    private class UniqueViolationNotificationDbContext : NotificationDbContext
    {
        public UniqueViolationNotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<NotificationEntity>().Any())
            {
                var inner = new Exception("Violation of UNIQUE KEY constraint. Cannot insert duplicate key");
                throw new DbUpdateException("Error saving", inner);
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public RedisConsumerTests()
    {
        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDb.Object);

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new NotificationDbContext(options);

        _mockClientProxy.Setup(x => x.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private RedisStreamsConsumer CreateConsumer(NotificationDbContext dbContext)
    {
        var mockProvider = new Mock<IServiceProvider>();
        mockProvider.Setup(x => x.GetService(typeof(IIdentityDirectoryClient))).Returns(_mockIdentityClient.Object);
        mockProvider.Setup(x => x.GetService(typeof(NotificationDbContext))).Returns(dbContext);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(mockProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

        return new RedisStreamsConsumer(_mockRedis.Object, mockScopeFactory.Object, mockHubContext.Object, _mockLogger.Object);
    }

    private static StreamEntry CreateStreamEntry(Guid eventId, Guid reportId, string eventType = "ReportSubmittedV1", string schemaVersion = "v1", string? customPayload = null)
    {
        string payload;
        if (customPayload != null)
        {
            payload = customPayload;
        }
        else
        {
            var envelope = new IntegrationEventEnvelopeV1(
                eventId,
                eventType,
                schemaVersion,
                DateTime.UtcNow,
                "report-service",
                "correlation-id",
                new { ReportId = reportId, ClubId = Guid.NewGuid(), ReporterId = Guid.NewGuid(), ReportType = "Weekly", Period = "Q1", SubmittedAtUtc = DateTime.UtcNow }
            );
            payload = JsonSerializer.Serialize(envelope);
        }

        return new StreamEntry(
            "12345-0",
            new[] { new NameValueEntry("envelope", payload) }
        );
    }

    private async Task InvokeProcessAsync(RedisStreamsConsumer consumer, StreamEntry entry, NotificationDbContext dbContext)
    {
        var method = typeof(RedisStreamsConsumer).GetMethod("ProcessAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException("Could not find ProcessAsync method.");

        var task = (Task)method.Invoke(consumer, new object[] { _mockDb.Object, entry, CancellationToken.None })!;
        await task;

        // If a failure was recorded in the database, throw it to make it visible
        var failure = await dbContext.StreamProcessingFailures.FirstOrDefaultAsync();
        if (failure != null)
        {
            throw new Exception($"ProcessAsync recorded a failure: {failure.LastErrorCode} - {failure.LastErrorMessage}");
        }
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_PersistsAndAcknowledges()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        _mockIdentityClient.Setup(x => x.ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recipient });

        var entry = CreateStreamEntry(eventId, reportId);
        var consumer = CreateConsumer(_dbContext);

        // Act
        await InvokeProcessAsync(consumer, entry, _dbContext);

        // Assert
        var notification = await _dbContext.Notifications.SingleOrDefaultAsync(x => x.SourceEventId == eventId);
        Assert.NotNull(notification);
        Assert.Equal(recipient, notification.UserId);
        Assert.Equal(reportId, notification.ReferenceId);

        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            RedisStreamsConsumer.Stream,
            RedisStreamsConsumer.Group,
            entry.Id,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_IdentityServiceUnavailable_DoesNotAck()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        _mockIdentityClient.Setup(x => x.ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("gRPC is down"));

        var entry = CreateStreamEntry(eventId, reportId);
        var consumer = CreateConsumer(_dbContext);

        // Act
        await InvokeProcessAsync(consumer, entry, _dbContext);

        // Assert
        var notification = await _dbContext.Notifications.SingleOrDefaultAsync(x => x.SourceEventId == eventId);
        Assert.Null(notification); // DB is not written

        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Never); // Not acknowledged
    }

    [Fact]
    public async Task ProcessAsync_DbCommitFails_DoesNotAck()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        _mockIdentityClient.Setup(x => x.ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recipient });

        var entry = CreateStreamEntry(eventId, reportId);

        var mockOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var failingContext = new FailingNotificationDbContext(mockOptions);

        var consumer = CreateConsumer(failingContext);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => InvokeProcessAsync(consumer, entry, failingContext));

        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Never); // Not acknowledged
    }

    [Fact]
    public async Task ProcessAsync_DuplicateDelivery_GracefullyIgnoredAndAcked()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        _mockIdentityClient.Setup(x => x.ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recipient });

        var entry = CreateStreamEntry(eventId, reportId);
        var consumer = CreateConsumer(_dbContext);

        // First delivery
        await InvokeProcessAsync(consumer, entry, _dbContext);

        // Mock database unique violation on second delivery using the custom Failing DbContext subclass
        var mockOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var uniqueViolationContext = new UniqueViolationNotificationDbContext(mockOptions);

        var consumerDuplicate = CreateConsumer(uniqueViolationContext);

        // Act - Second delivery
        await InvokeProcessAsync(consumerDuplicate, entry, uniqueViolationContext);

        // Assert - verified that it caught the violation, skipped insert, and called ACK
        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            RedisStreamsConsumer.Stream,
            RedisStreamsConsumer.Group,
            entry.Id,
            CommandFlags.None), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_SignalRThrowsException_NotificationPersistedAndAcked()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        _mockIdentityClient.Setup(x => x.ListActiveUsersBySystemRoleAsync("StudentAffairsAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recipient });

        _mockClientProxy.Setup(x => x.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR connection dropped"));

        var entry = CreateStreamEntry(eventId, reportId);
        var consumer = CreateConsumer(_dbContext);

        // Act
        await InvokeProcessAsync(consumer, entry, _dbContext);

        // Assert
        var notification = await _dbContext.Notifications.SingleOrDefaultAsync(x => x.SourceEventId == eventId);
        Assert.NotNull(notification); // Remains persisted

        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            RedisStreamsConsumer.Stream,
            RedisStreamsConsumer.Group,
            entry.Id,
            CommandFlags.None), Times.Once); // Acknowledged despite SignalR fail
    }

    [Fact]
    public async Task ProcessAsync_MalformedPayload_RecordsFailureAndEventuallyDlq()
    {
        // Arrange
        var entry = CreateStreamEntry(Guid.NewGuid(), Guid.NewGuid(), customPayload: "invalid json string {[[");
        var consumer = CreateConsumer(_dbContext);

        // Act - Call 4 times, failure record is created and incremented
        for (int i = 1; i <= 4; i++)
        {
            // Invoke directly to bypass failure checking (since we expect failures here)
            var method = typeof(RedisStreamsConsumer).GetMethod("ProcessAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task)method!.Invoke(consumer, new object[] { _mockDb.Object, entry, CancellationToken.None })!;
            await task;

            var failure = await _dbContext.StreamProcessingFailures.SingleOrDefaultAsync(x => x.StreamEntryId == entry.Id);
            Assert.NotNull(failure);
            Assert.Equal(i, failure.RetryCount);
        }

        // Verify it was never DLQ'ed or ACK'ed yet
        _mockDb.Verify(x => x.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<NameValueEntry[]>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<int?>(),
            It.IsAny<StreamTrimMode>(),
            It.IsAny<CommandFlags>()), Times.Never);

        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Never);

        // Act - 5th call: reaches max retry threshold
        var method5 = typeof(RedisStreamsConsumer).GetMethod("ProcessAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task5 = (Task)method5!.Invoke(consumer, new object[] { _mockDb.Object, entry, CancellationToken.None })!;
        await task5;

        // Assert
        // Reached limit, failure tracker should be cleaned up
        var finalFailure = await _dbContext.StreamProcessingFailures.SingleOrDefaultAsync(x => x.StreamEntryId == entry.Id);
        Assert.Null(finalFailure);

        // Written to DLQ
        _mockDb.Verify(x => x.StreamAddAsync(
            It.Is<RedisKey>(k => k == RedisStreamsConsumer.Dlq),
            It.Is<NameValueEntry[]>(arr => 
                arr.Any(n => n.Name == "originalStreamId" && n.Value.ToString() == entry.Id.ToString()) &&
                arr.Any(n => n.Name == "errorCode" && n.Value.ToString() == "JsonException")
            ),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<int?>(),
            It.IsAny<StreamTrimMode>(),
            It.IsAny<CommandFlags>()), Times.Once);

        // Acknowledged in primary stream
        _mockDb.Verify(x => x.StreamAcknowledgeAsync(
            RedisStreamsConsumer.Stream,
            RedisStreamsConsumer.Group,
            entry.Id,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RedisConnectionException_RetriesWithDelayAndExitsOnCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        int callCount = 0;

        _mockDb.Setup(x => x.StreamCreateConsumerGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()
        )).ReturnsAsync(true);

        _mockDb.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>()
        )).ReturnsAsync(RedisResult.Create(new RedisResult[] { RedisResult.Create((RedisValue)"0-0"), RedisResult.Create(Array.Empty<RedisResult>()) }));

        _mockDb.Setup(x => x.StreamReadGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CommandFlags>()
        )).Returns(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromException<StreamEntry[]>(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test connection failure"));
            }
            else
            {
                cts.Cancel();
                return Task.FromException<StreamEntry[]>(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test connection failure"));
            }
        });

        var consumer = CreateConsumer(_dbContext);
        consumer.ReconnectDelayMs = 10; // Fast reconnect for testing
        var method = typeof(RedisStreamsConsumer).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var executeTask = (Task)method!.Invoke(consumer, new object[] { cts.Token })!;
        await Task.WhenAny(executeTask, Task.Delay(2000));

        if (executeTask.IsFaulted)
        {
            throw executeTask.Exception!;
        }

        // Assert
        Assert.True(cts.IsCancellationRequested);
        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_RedisTimeoutException_RetriesWithDelayAndExitsOnCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        int callCount = 0;

        _mockDb.Setup(x => x.StreamCreateConsumerGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()
        )).ReturnsAsync(true);

        _mockDb.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>()
        )).ReturnsAsync(RedisResult.Create(new RedisResult[] { RedisResult.Create((RedisValue)"0-0"), RedisResult.Create(Array.Empty<RedisResult>()) }));

        _mockDb.Setup(x => x.StreamReadGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CommandFlags>()
        )).Returns(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromException<StreamEntry[]>(new RedisTimeoutException("Test Redis timeout", CommandStatus.Unknown));
            }
            else
            {
                cts.Cancel();
                return Task.FromException<StreamEntry[]>(new RedisTimeoutException("Test Redis timeout", CommandStatus.Unknown));
            }
        });

        var consumer = CreateConsumer(_dbContext);
        consumer.ReconnectDelayMs = 10; // Fast reconnect for testing
        var method = typeof(RedisStreamsConsumer).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var executeTask = (Task)method!.Invoke(consumer, new object[] { cts.Token })!;
        await Task.WhenAny(executeTask, Task.Delay(2000));

        if (executeTask.IsFaulted)
        {
            throw executeTask.Exception!;
        }

        // Assert
        Assert.True(cts.IsCancellationRequested);
        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_RecoveryOnRedisRestored_ResumesProcessing()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        int callCount = 0;
        bool processed = false;

        _mockDb.Setup(x => x.StreamCreateConsumerGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()
        )).ReturnsAsync(true);

        _mockDb.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>()
        )).ReturnsAsync(RedisResult.Create(new RedisResult[] { RedisResult.Create((RedisValue)"0-0"), RedisResult.Create(Array.Empty<RedisResult>()) }));

        _mockDb.Setup(x => x.StreamReadGroupAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CommandFlags>()
        )).Returns(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromException<StreamEntry[]>(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test connection failure"));
            }
            processed = true;
            cts.Cancel();
            return Task.FromResult(Array.Empty<StreamEntry>());
        });

        var consumer = CreateConsumer(_dbContext);
        consumer.ReconnectDelayMs = 10; // Fast reconnect for testing
        var method = typeof(RedisStreamsConsumer).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var executeTask = (Task)method!.Invoke(consumer, new object[] { cts.Token })!;
        await Task.WhenAny(executeTask, Task.Delay(2000));

        if (executeTask.IsFaulted)
        {
            throw executeTask.Exception!;
        }

        // Assert
        Assert.True(processed);
        Assert.True(cts.IsCancellationRequested);
    }
}
