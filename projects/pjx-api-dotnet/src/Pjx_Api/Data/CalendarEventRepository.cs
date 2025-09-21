using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pjx_Api.Data
{
    public class CalendarEventRepository : GenericRepository<CalendarEvent>, ICalendarEventRepository<CalendarEvent>
    {
        public CalendarEventRepository(CalendarDbContext context) : base(context) { }

        public List<CalendarEvent> GetAllBetweenByUser(string userId, DateTimeOffset start, DateTimeOffset end)
        {
            List<CalendarEvent> results = _context.CalendarEvents.Where(x =>
                x.UserId == userId
                && ((DateTimeOffset.Compare(x.Start, start) >= 0 && DateTimeOffset.Compare(x.Start, end) < 0)
                || ((x.End.HasValue && (DateTimeOffset.Compare(x.End.Value, start) >= 0 && DateTimeOffset.Compare(x.End.Value, end) < 0))
                || (!x.End.HasValue && (DateTimeOffset.Compare(x.Start, start.AddDays(-1)) >= 0 && DateTimeOffset.Compare(x.Start, end.AddDays(-1)) < 0)))
                ) || (DateTimeOffset.Compare(x.Start, start) <= 0 && ((x.End.HasValue && DateTimeOffset.Compare(x.End.Value, end) >= 0) || (!x.End.HasValue && DateTimeOffset.Compare(x.Start, end.AddDays(-1)) >= 0)))
            ).ToList();

            return results;
        }
    }
}
