using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.ConflictChecks;
using Pjx.CalendarLibrary.Repositories;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CalendarEventServiceExtensions
    {
        public static IServiceCollection AddCalendareEventRepository(this IServiceCollection services, IConfigurationSection configSection)
        {
            // future: Organizations, Departments 
            services.AddTransient(
                typeof(ICalendarEventRepository<CalendarEvent>),
                Type.GetType(configSection.GetValue<string>("Pjx.CalendarLibrary.Repositories.ICalendarEventRepository"))
            );

            services.AddTransient(
                typeof(IUnitOfWork),
                Type.GetType(configSection.GetValue<string>("Pjx.CalendarLibrary.Repositories.IUnitOfWork"))
            );

            services.AddTransient(
                typeof(IOverlappingCheck),
                Type.GetType(configSection.GetValue<string>("Pjx.CalendarLibrary.ConflictChecks.IOverlappingCheck"))
            );

            services.AddTransient(
                typeof(IConflictCheck),
                Type.GetType(configSection.GetValue<string>("Pjx.CalendarLibrary.ConflictChecks.IConflictCheck"))
            );

            return services;
        }
    }
}
