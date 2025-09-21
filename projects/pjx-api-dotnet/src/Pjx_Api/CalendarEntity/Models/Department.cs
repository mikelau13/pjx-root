using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Pjx.CalendarEntity.Models
{
    public interface IDepartment
    {
        int DepartmentId { get; }
        string Name { get; }
        int OrganizationId { get; }
    }

    public class Department: IDepartment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DepartmentId { get; set;  }

        [StringLength(50)]
        public string Name { get; set; }

        public int OrganizationId { get; set; }
        public Organization Organization { get; set; }
    }
}
