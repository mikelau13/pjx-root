using Pjx.CalendarLibrary.Repositories;
using Pjx.CalendarEntity.Models;

namespace Pjx_Api.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CalendarDbContext _context;
        public ICalendarEventRepository<CalendarEvent> CalendarEvents { get; private set; }
        public IOrganizationRepository<Organization> Organizations { get; private set; }
        public IDepartmentRepository<Department> Departments { get; private set; }

        public UnitOfWork(CalendarDbContext context,
            ICalendarEventRepository<CalendarEvent> _ceRepo)
        {
            _context = context;

            // for now, I am only injecting CalenderEvent Repository for testing purpose
            // I might inject other repositories in the future when it is necessary
            CalendarEvents = _ceRepo;
            Organizations = new OrganizationRepository(_context);
            Departments = new DepartmentRepository(_context);
        }

        public int Complete()
        {
            return _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
