using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;
using Microsoft.Extensions.Options;

namespace BlogApp.Services
{
    public class ResendEmailSender : IEmailSender
    {
        private readonly ResendClient _client;

        public ResendEmailSender(
            ResendClient client,
            IOptions<ResendClientOptions> opts)
        {
            // ApiToken is already wired by DI
            _client = client;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var message = new EmailMessage
            {
                From = "Jack's Blog <onboarding@resend.dev>",
                To = email,
                Subject = subject,
                HtmlBody = htmlMessage
            };

            await _client.EmailSendAsync(message);
        }
    }
}
