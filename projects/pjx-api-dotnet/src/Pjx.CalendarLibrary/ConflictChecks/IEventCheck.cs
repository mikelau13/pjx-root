using Pjx.CalendarEntity.Models;
using System.Collections.Generic;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public interface IEventCheck
    {
        IEventCheck SetNext(IEventCheck handler);

        bool Check(List<CalendarEvent> events, ICalendarEvent ce);
    }
}
