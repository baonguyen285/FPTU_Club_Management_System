using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.API.Controllers;
using Notification.Application.DTOs;
using Notification.Application.Features.Notifications.Commands.DeleteNotification;
using Notification.Application.Features.Notifications.Commands.MarkAllAsRead;
using Notification.Application.Features.Notifications.Commands.MarkAsRead;
using Notification.Application.Features.Notifications.Queries.GetNotifications;
using Notification.Application.Features.Notifications.Queries.GetUnreadCount;
using Notification.Application.Mappings;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Persistence.Repositories;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Xunit;

namespace CommunicationReliability.Tests
{
    public class NotificationRestTests
    {
        private readonly IMapper _mapper;

        public NotificationRestTests()
        {
            var mapperMock = new Moq.Mock<IMapper>();
            mapperMock.Setup(m => m.Map<IEnumerable<NotificationDto>>(Moq.It.IsAny<IEnumerable<NotificationEntity>>()))
                .Returns((IEnumerable<NotificationEntity> source) => source.Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type.ToString(),
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    ReferenceId = n.ReferenceId,
                    TargetUrl = n.TargetUrl,
                    SourceEventId = n.SourceEventId,
                    CreatedAt = n.CreatedAt
                }));
            _mapper = mapperMock.Object;
        }

        private NotificationDbContext CreateDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new NotificationDbContext(options);
        }

        [Fact]
        public async Task GetMyNotifications_ReturnsOnlyUserNotifications_AndFiltersSoftDeleted()
        {
            // Arrange
            using var db = CreateDbContext(nameof(GetMyNotifications_ReturnsOnlyUserNotifications_AndFiltersSoftDeleted));
            var unitOfWork = new UnitOfWork(db);
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            var n1 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userA, Title = "A1", Message = "Msg1", Type = NotificationType.SystemAlert, IsRead = false, IsDeleted = false, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
            var n2 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userA, Title = "A2", Message = "Msg2", Type = NotificationType.ReportSubmitted, IsRead = true, IsDeleted = false, CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
            var nDeleted = new NotificationEntity { Id = Guid.NewGuid(), UserId = userA, Title = "A-Deleted", Message = "MsgDel", Type = NotificationType.SystemAlert, IsRead = false, IsDeleted = true, CreatedAt = DateTime.UtcNow };
            var nUserB = new NotificationEntity { Id = Guid.NewGuid(), UserId = userB, Title = "B1", Message = "MsgB", Type = NotificationType.SystemAlert, IsRead = false, IsDeleted = false, CreatedAt = DateTime.UtcNow };

            db.Notifications.AddRange(n1, n2, nDeleted, nUserB);
            await db.SaveChangesAsync();

            var query = new GetNotificationsQuery { UserId = userA, Page = 1, PageSize = 10 };
            var handler = new GetNotificationsQueryHandler(unitOfWork, _mapper);

            // Act
            var (items, totalItems, unreadCount) = await handler.Handle(query, CancellationToken.None);

            // Assert
            var list = items.ToList();
            Assert.Equal(2, totalItems);
            Assert.Equal(1, unreadCount);
            Assert.Equal(2, list.Count);
            Assert.All(list, n => Assert.Equal(userA, n.UserId));
            Assert.DoesNotContain(list, n => n.Title == "A-Deleted");
            Assert.DoesNotContain(list, n => n.Title == "B1");
            Assert.Equal("A2", list[0].Title); // descending sort
        }

        [Fact]
        public async Task GetUnreadCount_ReturnsExactCount_AndFiltersDeleted()
        {
            // Arrange
            using var db = CreateDbContext(nameof(GetUnreadCount_ReturnsExactCount_AndFiltersDeleted));
            var unitOfWork = new UnitOfWork(db);
            var userId = Guid.NewGuid();

            var nUnread1 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userId, Title = "U1", Message = "M1", IsRead = false, IsDeleted = false, CreatedAt = DateTime.UtcNow };
            var nUnread2 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userId, Title = "U2", Message = "M2", IsRead = false, IsDeleted = false, CreatedAt = DateTime.UtcNow };
            var nRead = new NotificationEntity { Id = Guid.NewGuid(), UserId = userId, Title = "R1", Message = "M3", IsRead = true, IsDeleted = false, CreatedAt = DateTime.UtcNow };
            var nDeletedUnread = new NotificationEntity { Id = Guid.NewGuid(), UserId = userId, Title = "D1", Message = "M4", IsRead = false, IsDeleted = true, CreatedAt = DateTime.UtcNow };

            db.Notifications.AddRange(nUnread1, nUnread2, nRead, nDeletedUnread);
            await db.SaveChangesAsync();

            var handler = new GetUnreadCountQueryHandler(unitOfWork);

            // Act
            var count = await handler.Handle(new GetUnreadCountQuery { UserId = userId }, CancellationToken.None);

            // Assert
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task MarkAsRead_UpdatesIsReadAndReadAt_AndIsIdempotent()
        {
            // Arrange
            using var db = CreateDbContext(nameof(MarkAsRead_UpdatesIsReadAndReadAt_AndIsIdempotent));
            var unitOfWork = new UnitOfWork(db);
            var userId = Guid.NewGuid();
            var notifId = Guid.NewGuid();

            var notification = new NotificationEntity
            {
                Id = notifId,
                UserId = userId,
                Title = "Test",
                Message = "Msg",
                IsRead = false,
                ReadAt = null,
                CreatedAt = DateTime.UtcNow
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            var handler = new MarkAsReadCommandHandler(unitOfWork);
            var command = new MarkAsReadCommand { Id = notifId, UserId = userId };

            // Act 1: Mark as read
            var result1 = await handler.Handle(command, CancellationToken.None);

            // Assert 1
            Assert.True(result1);
            var updated = await db.Notifications.FindAsync(notifId);
            Assert.NotNull(updated);
            Assert.True(updated!.IsRead);
            Assert.NotNull(updated.ReadAt);

            var firstReadAt = updated.ReadAt;

            // Act 2: Repeat mark as read (idempotency check)
            var result2 = await handler.Handle(command, CancellationToken.None);

            // Assert 2
            Assert.True(result2);
            var updated2 = await db.Notifications.FindAsync(notifId);
            Assert.True(updated2!.IsRead);
            Assert.Equal(firstReadAt, updated2.ReadAt);
        }

        [Fact]
        public async Task MarkAsRead_OtherUserNotification_ThrowsNotFoundException()
        {
            // Arrange
            using var db = CreateDbContext(nameof(MarkAsRead_OtherUserNotification_ThrowsNotFoundException));
            var unitOfWork = new UnitOfWork(db);
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();
            var notifId = Guid.NewGuid();

            db.Notifications.Add(new NotificationEntity
            {
                Id = notifId,
                UserId = ownerId,
                Title = "Private",
                Message = "Secret",
                IsRead = false
            });
            await db.SaveChangesAsync();

            var handler = new MarkAsReadCommandHandler(unitOfWork);
            var command = new MarkAsReadCommand { Id = notifId, UserId = attackerId };

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task MarkAllAsRead_UpdatesOnlyAuthenticatedUserNotifications()
        {
            // Arrange
            using var db = CreateDbContext(nameof(MarkAllAsRead_UpdatesOnlyAuthenticatedUserNotifications));
            var unitOfWork = new UnitOfWork(db);
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            var nA1 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userA, Title = "A1", Message = "M1", IsRead = false };
            var nA2 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userA, Title = "A2", Message = "M2", IsRead = false };
            var nB1 = new NotificationEntity { Id = Guid.NewGuid(), UserId = userB, Title = "B1", Message = "M3", IsRead = false };

            db.Notifications.AddRange(nA1, nA2, nB1);
            await db.SaveChangesAsync();

            var handler = new MarkAllAsReadCommandHandler(unitOfWork);
            var command = new MarkAllAsReadCommand { UserId = userA };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result);
            var userANotifs = await db.Notifications.Where(n => n.UserId == userA).ToListAsync();
            Assert.All(userANotifs, n => Assert.True(n.IsRead));
            Assert.All(userANotifs, n => Assert.NotNull(n.ReadAt));

            var userBNotif = await db.Notifications.FindAsync(nB1.Id);
            Assert.False(userBNotif!.IsRead);
        }

        [Fact]
        public async Task DeleteNotification_SoftDeletes_AndExcludesFromQueries()
        {
            // Arrange
            using var db = CreateDbContext(nameof(DeleteNotification_SoftDeletes_AndExcludesFromQueries));
            var unitOfWork = new UnitOfWork(db);
            var userId = Guid.NewGuid();
            var notifId = Guid.NewGuid();

            var notif = new NotificationEntity
            {
                Id = notifId,
                UserId = userId,
                Title = "ToDelete",
                Message = "Msg",
                IsRead = false,
                IsDeleted = false
            };
            db.Notifications.Add(notif);
            await db.SaveChangesAsync();

            var deleteHandler = new DeleteNotificationCommandHandler(unitOfWork);
            var getHandler = new GetNotificationsQueryHandler(unitOfWork, _mapper);
            var unreadHandler = new GetUnreadCountQueryHandler(unitOfWork);

            // Act: Soft Delete
            var deleteResult = await deleteHandler.Handle(new DeleteNotificationCommand { Id = notifId, UserId = userId }, CancellationToken.None);

            // Assert: DB entity state
            Assert.True(deleteResult);
            var rawInDb = await db.Notifications.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.Id == notifId);
            Assert.NotNull(rawInDb);
            Assert.True(rawInDb!.IsDeleted);
            Assert.NotNull(rawInDb.DeletedAt);

            // Assert: Excluded from query & count
            var (items, totalItems, unreadCount) = await getHandler.Handle(new GetNotificationsQuery { UserId = userId }, CancellationToken.None);
            Assert.Equal(0, totalItems);
            Assert.Equal(0, unreadCount);
            Assert.Empty(items);
        }

        [Fact]
        public async Task DeleteNotification_OtherUser_ThrowsNotFoundException()
        {
            // Arrange
            using var db = CreateDbContext(nameof(DeleteNotification_OtherUser_ThrowsNotFoundException));
            var unitOfWork = new UnitOfWork(db);
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();
            var notifId = Guid.NewGuid();

            db.Notifications.Add(new NotificationEntity
            {
                Id = notifId,
                UserId = ownerId,
                Title = "Private",
                Message = "Msg",
                IsDeleted = false
            });
            await db.SaveChangesAsync();

            var handler = new DeleteNotificationCommandHandler(unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteNotificationCommand { Id = notifId, UserId = attackerId }, CancellationToken.None));
        }
    }
}
