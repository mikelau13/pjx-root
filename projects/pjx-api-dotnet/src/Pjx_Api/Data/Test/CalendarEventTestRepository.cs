using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pjx_Api.Data
{
    public class CalendarEventTestRepository : GenericRepository<CalendarEvent>, ICalendarEventRepository<CalendarEvent>
    {
        public CalendarEventTestRepository(CalendarDbContext context) : base(context) { }

        public List<CalendarEvent> GetAllBetweenByUser(string userId, DateTimeOffset start, DateTimeOffset end)
        {
            List<CalendarEvent> results = new List<CalendarEvent>
            {
                new CalendarEvent { EventId = 1, UserId = "testuser", DepartmentId = 1, Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()) },
                new CalendarEvent { EventId = 2, UserId = "testuser", DepartmentId = 1, Start = new DateTimeOffset(2020, 1, 3, 0, 0, 0, new TimeSpan()) }
            };

            return results;
        }
    }
}
