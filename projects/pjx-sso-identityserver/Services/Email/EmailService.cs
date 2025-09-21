using IdentityServer4.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace IdentityServerAspNetIdentity.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailConfig ec;
        private readonly string SmtpPassword;
        private readonly ILogger Logger;

        public string LocalDomain { get { return ec.LocalDomain; } }

        public EmailService(ILogger<EmailService> logger,
            IOptions<EmailConfig> emailConfig
            , SecretsManager secrets)
        {
            this.Logger = logger;
            this.ec = emailConfig.Value;
            // TODO: for now, putting the option to put secrets in either secret 
            // manager or appsettings.json; eventually we should do a better secret
            // management
            this.SmtpPassword = secrets.SmtpPassword ?? this.ec.UserPassword;
        }

        public async Task SendEmailAsync(String to, String subject, String message)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(this.SmtpPassword) && !this.SmtpPassword.Contains("********"))
                {
                    using (var mail = new MailMessage())
                    {
                        mail.To.Add(new MailAddress(to));
                        mail.From = new MailAddress(ec.FromAddress, ec.FromName);
                        mail.Subject = subject;
                        mail.Body = message;
                        mail.IsBodyHtml = true;

                        using (var client = new SmtpClient(ec.MailServerAddress))
                        {
                            client.Port = ec.MailServerPort;
                            client.Credentials = new NetworkCredential(ec.UserId, this.SmtpPassword);
                            client.EnableSsl = true;
                            await client.SendMailAsync(mail).ConfigureAwait(false);
                            Logger.LogInformation("SendEmailAsync succeed: " + subject);
                        }
                    }
                }
                else
                {
                    Logger.LogInformation("====Smtp disabled====");
                    Logger.LogInformation("to: " + to);
                    Logger.LogInformation("subject: " + subject);
                    Logger.LogInformation("body: " + message);
                }
            }
            catch (Exception ex)
            {
                Logger.LogInformation("SendEmailAsync error: " + ex.Message);
            }
        }
    }
}
