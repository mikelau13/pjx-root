using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public interface IConflictCheck
    {
        bool DoCheck(ICalendarEvent ce);
    }
}
