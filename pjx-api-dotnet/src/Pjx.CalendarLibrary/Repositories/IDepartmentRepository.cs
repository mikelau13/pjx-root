using Pjx.CalendarEntity.Models;
using System.Collections.Generic;

namespace Pjx.CalendarLibrary.Repositories
{
    public interface IDepartmentRepository<T> : IGenericRepository<T>
        where T : class, IDepartment
    {
        IEnumerable<T> GetByOrganization(int orgId);
    }
}
