using Microsoft.AspNetCore.Identity.UI.Services;
using MyCookbook.Logging;
using System.Reflection;
using RestSharp;
using RestSharp.Authenticators;

namespace MyCookbook.Services;
public class MailgunEmailSender : IEmailSender
{
    private readonly ILogger _logger;
    private readonly RestClient _client;
    private readonly string _fromEmail;
    private readonly string _mailDomain;

    public MailgunEmailSender(ILogger<MailgunEmailSender> logger, IConfiguration config)
    {
        var options = new RestClientOptions("https://api.eu.mailgun.net")
        {
            Authenticator = new HttpBasicAuthenticator("api", config["Mailgun:ApiKey"])
        };

        _client = new RestClient(options);
        _fromEmail = config["Mailgun:FromEmail"];
        _mailDomain = config["Mailgun:MailDomain"];

        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
        var request = new RestRequest($"/v3/{_mailDomain}/messages", Method.Post);
        request.AlwaysMultipartFormData = true;

        request.AddParameter("from", _fromEmail);
        request.AddParameter("to", toEmail);
        request.AddParameter("subject", subject);
        request.AddParameter("text", message);

        var response = await _client.ExecuteAsync(request);

        _logger.LogInformation(response.IsSuccessful
                               ? $"Email to {toEmail} queued successfully!"
                               : $"Failure Email to {toEmail}: {response.ErrorMessage}");
    }
}