using Pjx.CalendarEntity.Models;
using System;
using System.Collections.Generic;

namespace Pjx.CalendarLibrary.Repositories
{
    public interface ICalendarEventRepository<T>: IGenericRepository<T>
        where T: class, ICalendarEvent
    {
        List<T> GetAllBetweenByUser(string userId, DateTimeOffset start, DateTimeOffset end);
    }
}
