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
            // ConflictCheck passes MinValue/MaxValue to mean "every event for this user",
            // so AddDays(-1) can underflow past DateTimeOffset.MinValue. EF Core 8
            // evaluates these sub-expressions eagerly to build SQL parameters; EF Core
            // 3.1 never did, which is why this only started throwing after the upgrade.
            DateTimeOffset startPrev = start > DateTimeOffset.MinValue.AddDays(1)
                ? start.AddDays(-1) : DateTimeOffset.MinValue;
            DateTimeOffset endPrev = end > DateTimeOffset.MinValue.AddDays(1)
                ? end.AddDays(-1) : DateTimeOffset.MinValue;

            List<CalendarEvent> results = _context.CalendarEvents.Where(x =>
                x.UserId == userId
                && ((DateTimeOffset.Compare(x.Start, start) >= 0 && DateTimeOffset.Compare(x.Start, end) < 0)
                || ((x.End.HasValue && (DateTimeOffset.Compare(x.End.Value, start) >= 0 && DateTimeOffset.Compare(x.End.Value, end) < 0))
                || (!x.End.HasValue && (DateTimeOffset.Compare(x.Start, startPrev) >= 0 && DateTimeOffset.Compare(x.Start, endPrev) < 0)))
                ) || (DateTimeOffset.Compare(x.Start, start) <= 0 && ((x.End.HasValue && DateTimeOffset.Compare(x.End.Value, end) >= 0) ||
(!x.End.HasValue && DateTimeOffset.Compare(x.Start, endPrev) >= 0)))
            ).ToList();

            return results;
        }
    }
}
