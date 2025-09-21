using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public class ConflictCheckChain
    {
        private IConflictCheck handler;

        public ConflictCheckChain(IConflictCheck _handler)
        {
            handler = _handler;
        }

        public bool Execute(List<CalendarEvent> events, ICalendarEvent ce)
        {
            return handler.Check(events, ce);
        }
    }
}
