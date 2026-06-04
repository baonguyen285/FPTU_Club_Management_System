using Auth.Domain.Entities;

namespace Auth.Application.Services
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
    }
}
