using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pjx.CalendarEntity.Models;
using System;

namespace Pjx_Api.Data
{
    public class CalendarDbContext: DbContext
    {
        public CalendarDbContext(DbContextOptions<CalendarDbContext> options) : base(options) { }

        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Department> Departments { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Npgsql requires Offset=0 for 'timestamp with time zone'. Converting here
            // keeps every write UTC without scattering ToUniversalTime() through the
            // controllers and the conflict-check code. SQLite accepted any offset, which
            // is why this only appeared after the PostgreSQL swap.
            var toUtc = new ValueConverter<DateTimeOffset, DateTimeOffset>(
                v => v.ToUniversalTime(),
                v => v);

            var toUtcNullable = new ValueConverter<DateTimeOffset?, DateTimeOffset?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v);

            modelBuilder.Entity<CalendarEvent>().Property(e => e.Start).HasConversion(toUtc);
            modelBuilder.Entity<CalendarEvent>().Property(e => e.End).HasConversion(toUtcNullable);

            modelBuilder.Entity<CalendarEvent>()
                .ToTable("CalendarEvents")
                .HasIndex(b => b.UserId)
                .HasName("Index_UserId");

            modelBuilder.Entity<Organization>()
                .HasIndex(b => b.OrganizationId)
                .HasName("Index_OrganizationId");

            modelBuilder.Entity<Department>()
                .HasIndex(b => b.DepartmentId)
                .HasName("Index_DepartmentId");

            modelBuilder.Entity<Organization>()
                .HasData(
                    new Organization { OrganizationId = 1, Name = "Default Organization" }
                );

            modelBuilder.Entity<Department>()
                .HasData(
                    new Department { DepartmentId = 1, Name = "Default Department A", OrganizationId = 1 },
                    new Department { DepartmentId = 2, Name = "Default Department B", OrganizationId = 1 }
                );
        }
    }
}
