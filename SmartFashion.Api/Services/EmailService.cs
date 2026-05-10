using System.Net;
using System.Net.Mail;

namespace SmartFashion.Api.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config) => _config = config;

    public async Task SendAsync(string to, string subject, string body)
    {
        var host = Required("Smtp:Host");
        var port = int.Parse(Required("Smtp:Port"));
        var user = Required("Smtp:User");
        var pass = Required("Smtp:Pass");

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        var msg = new MailMessage(user, to, subject, body);
        await client.SendMailAsync(msg);
    }

    private string Required(string key)
    {
        var value = _config[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{key} is missing. Configure SMTP settings outside source control.");

        return value;
    }
}
