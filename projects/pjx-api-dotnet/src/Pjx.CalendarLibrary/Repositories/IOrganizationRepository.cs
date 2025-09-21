using Pjx.CalendarEntity.Models;
using System.Collections.Generic;

namespace Pjx.CalendarLibrary.Repositories
{
    public interface IOrganizationRepository<T> : IGenericRepository<T>
        where T : class, IOrganization
    {
        IEnumerable<T> GetByOwner(string userId);
    }
}
