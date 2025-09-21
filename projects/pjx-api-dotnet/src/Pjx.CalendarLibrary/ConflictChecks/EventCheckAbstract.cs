using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public abstract class EventCheckAbstract: IEventCheck
    {
        private IEventCheck _nextCheck;

        public IEventCheck SetNext(IEventCheck conflictCheck)
        {
            _nextCheck = conflictCheck;
            return conflictCheck;
        }

        public virtual bool Check(List<CalendarEvent> events, ICalendarEvent ce)
        {
            return _nextCheck.Check(events, ce);
        }
    }
}
