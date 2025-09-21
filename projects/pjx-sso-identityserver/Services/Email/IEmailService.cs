using System.Threading.Tasks;

namespace IdentityServerAspNetIdentity.Services.Email
{
    public interface IEmailService
    {
        string LocalDomain { get; }
        Task SendEmailAsync(string toEmail, string subject, string message);
    }
}
