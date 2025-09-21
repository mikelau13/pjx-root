using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public class ConflictCheck
    {
        private ConflictCheckChain _chain;
        private List<CalendarEvent> _events;

        public ConflictCheck(ICalendarEventRepository<CalendarEvent> repository
            , [Optional] IOverlappingCheck olc)
        {
            _events = repository.GetAll(); // The idea is that, there will be many rule checks (my previous projects have over 30 rules) as well as many events (aka 1000+), so we do not want to connect to database multiple times

            IConflictCheck handler = BuildCheckChain(olc);

            handler.SetNext(new NullCheck()); // Null check always the last one
            _chain = new ConflictCheckChain(handler);
        }

        public bool DoCheck(ICalendarEvent ce)
        {
            return _chain.Execute(_events, ce);
        }

        private IConflictCheck BuildCheckChain(IConflictCheck olc)
        {
            IConflictCheck handler = olc;

            handler.SetNext(new NullCheck());

            return handler;
        }
    }
}
