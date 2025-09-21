using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public interface IOverlappingCheck : IEventCheck { }

    public class OverlappingCheck: EventCheckAbstract, IOverlappingCheck
    {
        public override bool Check(List<CalendarEvent> events, ICalendarEvent ce)
        {
            if (events.Count != 0)
            {
                DateTimeOffset end;
                if (!ce.End.HasValue) // full day event
                {
                    end = ce.Start.AddDays(1);
                } 
                else
                {
                    end = ce.End.Value;
                }

                bool overlapped = events.Exists(x => x.EventId != ce.EventId 
                    && (((DateTimeOffset.Compare(x.Start, ce.Start) >= 0 && DateTimeOffset.Compare(x.Start, end) < 0)
                        || ((x.End.HasValue && (DateTimeOffset.Compare(x.End.Value, ce.Start) > 0 && DateTimeOffset.Compare(x.End.Value, end) < 0))
                        || (!x.End.HasValue && (DateTimeOffset.Compare(x.Start.AddDays(1), ce.Start) > 0 && DateTimeOffset.Compare(x.Start.AddDays(1), end) < 0)))
                        ) || (DateTimeOffset.Compare(x.Start, ce.Start) <= 0 && ((x.End.HasValue && DateTimeOffset.Compare(x.End.Value, end) >= 0) || (!x.End.HasValue && DateTimeOffset.Compare(x.Start.AddDays(1), end) >= 0)))
                    )
                );

                if (overlapped) return false;
            }

            return base.Check(events, ce);
        }
    }
}
