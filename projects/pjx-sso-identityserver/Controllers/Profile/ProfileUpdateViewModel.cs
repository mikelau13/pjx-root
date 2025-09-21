using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServerAspNetIdentity.Controllers.Profile
{
    public class ProfileUpdateViewModel
    {
        public bool IsUpdated { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string ReturnUrl { get;set; }
    }
}
