using System.Text.Json;
using Auth.API.Controllers;
using Auth.Application.DTOs;
using Auth.Application.Services;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class PublicRegistrationAuthorizationTests
{
    [Fact]
    public async Task Register_CreatesStudent()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = new RegisterRequest
        {
            Email = "new.student@fpt.edu.vn",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FullName = "New Student"
        };

        await controller.Register(request);

        var user = await context.Users.SingleAsync();
        Assert.Equal(SystemRoleNames.Student, user.Role);
    }

    [Theory]
    [InlineData(SystemRoleNames.ClubManager)]
    [InlineData(SystemRoleNames.StudentAffairsAdmin)]
    public async Task Register_IgnoresPrivilegedRoleInJsonPayload(string attemptedRole)
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var json = $$"""
        {
          "email": "attacker.{{attemptedRole}}@fpt.edu.vn",
          "password": "Password123!",
          "confirmPassword": "Password123!",
          "fullName": "Privilege Attempt",
          "role": "{{attemptedRole}}"
        }
        """;
        var request = JsonSerializer.Deserialize<RegisterRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);
        Assert.DoesNotContain(
            typeof(RegisterRequest).GetProperties(),
            property => property.Name.Equals("Role", StringComparison.OrdinalIgnoreCase));

        await controller.Register(request);

        var user = await context.Users.SingleAsync();
        Assert.Equal(SystemRoleNames.Student, user.Role);
    }

    private static AuthDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }

    private static AuthController CreateController(AuthDbContext context)
    {
        var jwtService = new Mock<IJwtService>();
        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(sender => sender.SendVerificationCodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return new AuthController(context, jwtService.Object, emailSender.Object);
    }
}
