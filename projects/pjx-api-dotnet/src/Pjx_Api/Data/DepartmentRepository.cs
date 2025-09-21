using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pjx_Api.Data
{
    public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository<Department>
    {
        public DepartmentRepository(CalendarDbContext context) : base(context) { }

        public IEnumerable<Department> GetByOrganization(int orgId)
        {
            List<Department> results = _context.Departments.Where(x =>
                x.OrganizationId == orgId
            ).ToList();

            return results;
        }
    }
}
