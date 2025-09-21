using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public interface IConflictCheck
    {
        IConflictCheck SetNext(IConflictCheck handler);

        bool Check(List<CalendarEvent> events, ICalendarEvent ce);
    }

    public abstract class EventCheckAbstract: IConflictCheck
    {
        private IConflictCheck _nextCheck;

        public IConflictCheck SetNext(IConflictCheck conflictCheck)
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
