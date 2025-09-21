using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using IdentityServerAspNetIdentity.Models;
using IdentityServerHost.Controllers.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerAspNetIdentity.Controllers.Profile
{
    [SecurityHeaders]
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ApplicationUser user = _userManager.FindByIdAsync(User.GetSubjectId()).Result;

            ProfileUpdateViewModel vm = new ProfileUpdateViewModel { 
                DisplayName = user.DisplayName,
                Email = user.Email
            };

            return View("Index", vm);
        }


        /// <summary>
        /// Handle postback from /profile update
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileUpdateModel model, string button)
        {
            switch(button)
            {
                case "login": return await ProfileUpdate(model);
                case "cancel": 
                    if (string.IsNullOrEmpty(model.ReturnUrl))
                    {
                        return Redirect("~/");
                    } 
                    else 
                    {
                        return Redirect(model.ReturnUrl);
                    }
                default: return Index();                
            }
        }

        private async Task<IActionResult> ProfileUpdate(ProfileUpdateModel model) 
        {
            ApplicationUser user = _userManager.FindByIdAsync(User.GetSubjectId()).Result;

            ProfileUpdateViewModel vm = new ProfileUpdateViewModel
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsUpdated = true,
                ReturnUrl =  model.ReturnUrl
            };

            if (ModelState.IsValid)
            {
                if (user != null)
                {
                    user.DisplayName = model.DisplayName;
                    await _userManager.UpdateAsync(user);
                    vm.IsUpdated = true;
                }
            }

            return View("Index", vm);
        }
    }
}
