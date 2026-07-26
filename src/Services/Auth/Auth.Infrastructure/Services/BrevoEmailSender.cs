using System.Net.Http.Json;
using Auth.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Exceptions;

namespace Auth.Infrastructure.Services
{
    public class BrevoEmailSender : IEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BrevoEmailSender> _logger;

        public BrevoEmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoEmailSender> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public Task SendVerificationCodeAsync(string email, string fullName, string code)
        {
            var subject = "Verify your FPTU Club account";
            var html = $"<p>Hello {fullName},</p><p>Your verification code is <strong>{code}</strong>.</p><p>This code expires in 15 minutes.</p>";
            return SendAsync(email, fullName, subject, html, code);
        }

        public Task SendPasswordResetCodeAsync(string email, string fullName, string code)
        {
            var subject = "Reset your FPTU Club password";
            var html = $"<p>Hello {fullName},</p><p>Your password reset code is <strong>{code}</strong>.</p><p>This code expires in 15 minutes.</p>";
            return SendAsync(email, fullName, subject, html, code);
        }

        private async Task SendAsync(string email, string fullName, string subject, string htmlContent, string code)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var senderEmail = _configuration["Brevo:SenderEmail"] ?? "no-reply@fptu-club.local";
            var senderName = _configuration["Brevo:SenderName"] ?? "FPTU Club";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Brevo API key is not configured. Email to {Email} was not sent. Development code: {Code}", email, code);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { email = senderEmail, name = senderName },
                to = new[] { new { email, name = fullName } },
                subject,
                htmlContent
            });

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brevo email send failed with status {StatusCode}: {Body}", response.StatusCode, body);
                throw new ServiceUnavailableException(
                    "Verification email service is temporarily unavailable. Please try again later.");
            }
        }
    }
}
