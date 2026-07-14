using System.Threading.Tasks;

namespace Auth.Application.Services
{
    public interface IEmailSender
    {
        Task SendVerificationCodeAsync(string email, string fullName, string code);
        Task SendPasswordResetCodeAsync(string email, string fullName, string code);
    }
}
