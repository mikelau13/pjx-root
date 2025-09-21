using Microsoft.AspNetCore.Identity;

namespace IdentityServerAspNetIdentity.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    // To update EF migration, run commands: 
    //  dotnet ef migrations add UpdateIdentitySchema -c ApplicationDbContext -o Data/Migrations
    //  dotnet ef database update
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }
    }
}
