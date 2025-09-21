using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pjx_Api.Data
{
    public class OrganizationRepository : GenericRepository<Organization>, IOrganizationRepository<Organization>
    {
        public OrganizationRepository(CalendarDbContext context) : base(context) { }

        public IEnumerable<Organization> GetByOwner(string userId)
        {
            List<Organization> results = _context.Organizations.Where(x =>
                x.OwnerUserId == userId
            ).ToList();

            return results;
        }
    }
}
