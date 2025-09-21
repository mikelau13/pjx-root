using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Pjx.CalendarLibrary.ConflictChecks
{
    public class ConflictCheck: IConflictCheck
    {
        private ConflictCheckChain _chain;
        private ICalendarEventRepository<CalendarEvent> _repository;

        public ConflictCheck(ICalendarEventRepository<CalendarEvent> repository
            , [Optional] IOverlappingCheck olc)
        {
            _repository = repository;

            IEventCheck handler = BuildCheckChain(olc);

            handler.SetNext(new NullCheck()); // Null check always the last one
            _chain = new ConflictCheckChain(handler);
        }

        public bool DoCheck(ICalendarEvent ce)
        {
            List<CalendarEvent> events = _repository.GetAllBetweenByUser(ce.UserId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

            return _chain.Execute(events, ce);
        }

        private IEventCheck BuildCheckChain(IEventCheck olc)
        {
            IEventCheck handler = olc;

            handler.SetNext(new NullCheck());

            return handler;
        }
    }
}
