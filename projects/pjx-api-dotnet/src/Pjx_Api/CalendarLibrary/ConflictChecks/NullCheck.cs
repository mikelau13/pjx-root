using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public class NullCheck: EventCheckAbstract
    {
        public override bool Check(List<CalendarEvent> events, ICalendarEvent ce)
        {
            return true;
        }
    }
}
