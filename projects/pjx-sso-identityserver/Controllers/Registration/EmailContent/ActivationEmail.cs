using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServerAspNetIdentity.Controllers.Registration.EmailContent
{
    public static class ActivationEmail
    {
        public static (string, string) GenerateContent(string activationUrl, string displayName, string emailAddress, string activationCode)
        {
            string subject = "IdentityServer4 Registration - " + displayName;

            string body = String.Format(@"Hi {2}
<br/>Click <a href='{3}/account/activate?code={0}&username={1}'>this link</a> to activate account.
<br/>
<br/>Or alternatively, copy and paste this Url to the browser's address bar: {3}/account/activate?code={0}&username={1}
", activationCode, emailAddress, displayName, activationUrl);

            return (subject, body);
        }
    }
}
