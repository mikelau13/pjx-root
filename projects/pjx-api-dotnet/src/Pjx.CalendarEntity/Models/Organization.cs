using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pjx.CalendarEntity.Models
{
    public interface IOrganization
    {
        int OrganizationId { get; }
        string OwnerUserId { get; }
        string Name { get; }
    }

    public class Organization: IOrganization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrganizationId { get; set; }

        [ConcurrencyCheck]
        public string OwnerUserId { get; set; }

        [StringLength(50)]
        public string Name { get; set; }

        public List<Department> Departments { get; set; }
    }
}
