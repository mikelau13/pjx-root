using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pjx.CalendarEntity.Models
{    
    public interface ICalendarEvent
    {
        int EventId { get; }
        string UserId { get; }
        string Title { get; }
        DateTimeOffset Start { get; }
        DateTimeOffset? End { get; }
    }


    [Serializable]
    public class CalendarEvent: ICalendarEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EventId { get; set; }

        [ConcurrencyCheck]
        public string UserId { get; set; }

        [StringLength(50)]
        public string Title { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset? End { get; set; }
        public int DepartmentId { get; set; }
        public Department Department { get; set; }
    }
}
