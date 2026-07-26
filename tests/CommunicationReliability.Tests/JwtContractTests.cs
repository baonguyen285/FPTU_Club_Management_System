using System.IdentityModel.Tokens.Jwt;
using Auth.Domain.Entities;
using Auth.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class JwtContractTests
{
    private static JwtService CreateService() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-only-secret-key-that-is-at-least-32-chars",
            ["JwtSettings:Issuer"] = "fptu-club-system-tests",
            ["JwtSettings:Audience"] = "fptu-club-clients-tests"
        }).Build());

    [Fact]
    public void AccessToken_UsesCanonicalClaimsAndContainsNoSensitiveData()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Student Affairs",
            Role = SystemRoleNames.StudentAffairsAdmin
        };

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService().GenerateAccessToken(user));
        var claims = token.Claims.ToDictionary(x => x.Type, x => x.Value);

        Assert.Equal(user.Id.ToString(), claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(user.Email, claims[JwtRegisteredClaimNames.Email]);
        Assert.Equal(user.FullName, claims[JwtRegisteredClaimNames.Name]);
        Assert.Equal(user.Role, claims["role"]);
        Assert.False(string.IsNullOrWhiteSpace(claims[JwtRegisteredClaimNames.Jti]));
        Assert.True(long.TryParse(claims[JwtRegisteredClaimNames.Iat], out _));
        Assert.Equal("fptu-club-system-tests", token.Issuer);
        Assert.Contains("fptu-club-clients-tests", token.Audiences);
        Assert.True(token.ValidTo > DateTime.UtcNow);
        Assert.DoesNotContain(token.Claims, x =>
            x.Type.Contains("membership", StringComparison.OrdinalIgnoreCase) ||
            x.Type.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            x.Type.Contains("refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AccessToken_GeneratesUniqueJti()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "student@fpt.edu.vn",
            FullName = "Student", Role = SystemRoleNames.Student
        };
        var handler = new JwtSecurityTokenHandler();
        var first = handler.ReadJwtToken(CreateService().GenerateAccessToken(user));
        var second = handler.ReadJwtToken(CreateService().GenerateAccessToken(user));

        Assert.NotEqual(
            first.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value);
    }
}
