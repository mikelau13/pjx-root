using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityServerAspNetIdentity.Controllers.Registration
{
    public class RegisterBindingModel
    {
        //[Required(ErrorMessage = "First name is required.")]
        //[StringLength(30)]
        //[Display(Name = "First Name")]
        //public string FirstName { get; set; }

        //[Required(ErrorMessage = "Last name is required.")]
        //[StringLength(30)]
        //[Display(Name = "Last Name")]
        //public string LastName { get; set; }

        [StringLength(30)]
        public string DisplayName { get; set; }

        //[Range(1916, 2050)]
        //[Display(Name = "Year of Birth")]
        //public int? BirthYear { get; set; }

        //[Display(Name = "Gender")]
        //public string Gender { get; set; }

        //[StringLength(7)]
        //[Display(Name = "Postal Code")]
        //public string PostalCode { get; set; }


        [Required(ErrorMessage = "Email ID is required.")]
        [Display(Name = "Email")]
        [StringLength(70)]
        [EmailAddress(ErrorMessage = "This value should be a valid email.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "Please provide a password that is between 5 and 50 characters in length.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        //[DataType(DataType.Password)]
        //[Display(Name = "Confirm password")]
        //[Compare("Password", ErrorMessage = "Password must match.")]
        //public string ConfirmPassword { get; set; }
    }
}
