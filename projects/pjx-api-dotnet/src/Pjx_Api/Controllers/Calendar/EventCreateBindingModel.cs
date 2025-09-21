using System;
using System.ComponentModel.DataAnnotations;

namespace Pjx_Api.Controllers.Calendar
{
    public class EventCreateBindingModel
    {
        [Required(ErrorMessage = "Title is required.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Start is required.")]
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset? End { get; set; }
    }
}
