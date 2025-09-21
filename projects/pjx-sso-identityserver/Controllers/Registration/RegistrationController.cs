using IdentityModel;
using IdentityServer4.Events;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServerAspNetIdentity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using IdentityServerHost.Controllers.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using IdentityServerAspNetIdentity.Services.Email;
using IdentityServerAspNetIdentity.Controllers.Registration.EmailContent;

namespace IdentityServerAspNetIdentity.Controllers.Registration
{
    [SecurityHeaders]
    [AllowAnonymous]
    public class RegistrationController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        //private readonly SignInManager<ApplicationUser> _signInManager;
        //private readonly IIdentityServerInteractionService _interaction;
        //private readonly IClientStore _clientStore;
        //private readonly IAuthenticationSchemeProvider _schemeProvider;
        //private readonly IEventService _events;

        public RegistrationController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IAuthenticationSchemeProvider schemeProvider,
            IEventService events,
            IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
            //_signInManager = signInManager;
            //_interaction = interaction;
            //_clientStore = clientStore;
            //_schemeProvider = schemeProvider;
            //_events = events;
        }


        /// <summary>
        /// Validate activation code to activate account.  Activation code can be used for one time only.
        /// </summary>
        /// <param name="username">Username of the account to validate</param>
        /// <param name="activationCode">Activation code</param>
        /// <returns>200 if succeed; otherwise return a bad request with error message.</returns>
        /// <seealso cref="HttpGetAttribute"/>
        [HttpGet]
        [Route("api/validate")]
        public async Task<IActionResult> ValidateEmail(string username, string activationCode)
        {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(activationCode))
            {
                ApplicationUser user = await _userManager.FindByEmailAsync(username);

                if (user != null)
                {
                    if (!user.EmailConfirmed)
                    {
                        IdentityResult result = _userManager.ConfirmEmailAsync(user, activationCode.Replace(" ", "+")).Result;

                        if (result.Succeeded)
                        {
                            user.EmailConfirmed = true;
         
                            await _userManager.UpdateAsync(user);

                            return Ok();
                        }
                        else
                        {
                            return BadRequest("Invalid validation code.");
                        }
                    }
                    else
                    {
                        return BadRequest("Invalid validation code.");
                    }
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return BadRequest("Username and validation code are required.");
            }
        }

        /// <summary>
        /// Allow users to register an new account.
        /// <br/>First to ensure the email address does not already exist.
        /// <br/>If succeed, this Api will send out an activation email to the users.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>200 if succeed; otherwise return a bad request with error message.</returns>
        /// <seealso cref="HttpPostAttribute"/>
        [HttpPost]
        [Route("api/register")]
        public async Task<IActionResult> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ApplicationUser user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null && !user.EmailConfirmed)
            {
                // there might be a special case where some users have problem with the activation code so they would re-register
                // for this reason, we better update the previous Activation Code
                user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Email : model.DisplayName;
                string activationCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _userManager.UpdateAsync(user);

                try
                {
                    var (subject, body) = ActivationEmail.GenerateContent(_emailService.LocalDomain, user.DisplayName, user.Email, activationCode);
                    // TODO: sending all email to myself for testing purpose, remove later
                    _ = Task.Run(() => _emailService.SendEmailAsync("mikelau13@hotmail.com", subject, body));

                    return Ok();
                }
                catch (Exception)
                {
                    return BadRequest("Re-registration saved, however error when sending email.");
                }
            }
            else
            {
                // first time registration
                IdentityResult result;
                CreateAccount(_userManager, model.Email, model.Password, model.DisplayName, out user, out result);

                if (!result.Succeeded)
                {
                    // failed to create, could be due to user already exist, or other reasons.
                    return GetErrorResult(result);
                }

                // update activate code, code generated from using the user.Id so must first create user then update the record
                string activationCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                //await _userManager.UpdateAsync(user);

                try
                {
                    var (subject, body) = ActivationEmail.GenerateContent(_emailService.LocalDomain, user.DisplayName, user.Email, activationCode);
                    // TODO: sending all email to myself for testing purpose, remove later
                    _ = Task.Run(() => _emailService.SendEmailAsync("mikelau13@hotmail.com", subject, body));

                    return Ok();
                }
                catch (Exception)
                {
                    return BadRequest("New user have been saved successfully, however returned error when sending email.");
                }
            }
        }


        internal static void CreateAccount(UserManager<ApplicationUser> userManager
            , string email, string password, string displayName
            , out ApplicationUser user, out IdentityResult result)
        {
            user = new ApplicationUser()
            {
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                LockoutEnabled = false,
            };

            result = userManager.CreateAsync(user, password).Result;
        }


        private IActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return StatusCode(500);
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (IdentityError error in result.Errors)
                    {
                        ModelState.AddModelError(error.Code, error.Description);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }
    }
}
