using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace MyCookbook.Services;
public class EmailSender : IEmailSender
{
    private readonly ILogger _logger;
    private readonly EmailClient _client;

    public EmailSender(ILogger<EmailSender> logger)
    {
        var connectionString = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_CONNECTION_STRING");
        _client = new EmailClient(connectionString);
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var response = await _client.SendAsync(
            WaitUntil.Completed,
            senderAddress: "DoNotReply@4d843932-02f9-456e-8dda-0ba3e0bf8f31.azurecomm.net",
            recipientAddress: toEmail,
            subject: subject,
            htmlContent: message);

        _logger.LogInformation(response.Value.Status == EmailSendStatus.Succeeded
                               ? $"Email to {toEmail} queued successfully!"
                               : $"Failure Email to {toEmail}");
    }
}