using System;
using System.ComponentModel.DataAnnotations;

namespace Pjx_Api.Controllers.Calendar
{
    public class EventReadAllBindingModel
    {
        [Required(ErrorMessage = "Start is required.")]
        public DateTimeOffset Start { get; set; }

        [Required(ErrorMessage = "End is required.")]
        public DateTimeOffset End { get; set; }
    }
}
