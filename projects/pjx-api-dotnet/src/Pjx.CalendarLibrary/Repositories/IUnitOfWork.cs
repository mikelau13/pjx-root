using System;
using Pjx.CalendarEntity.Models;

namespace Pjx.CalendarLibrary.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        ICalendarEventRepository<CalendarEvent> CalendarEvents { get; }
        IOrganizationRepository<Organization> Organizations { get; }
        IDepartmentRepository<Department> Departments { get; }
        int Complete();
    }
}
