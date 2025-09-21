using System.ComponentModel.DataAnnotations;

namespace IdentityServerAspNetIdentity.Controllers.Profile
{
    public class ProfileUpdateModel
    {
        [Required]
        public string DisplayName { get; set; }

        public string ReturnUrl { get; set; }
    }
}
