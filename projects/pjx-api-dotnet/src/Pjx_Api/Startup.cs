using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.ConflictChecks;
using Pjx.CalendarLibrary.Repositories;
using Pjx_Api.Data;

namespace Pjx_Api
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerDocument(config =>
            {
                config.DocumentProcessors.Add(new SecurityDefinitionAppender("JWT Token",
                new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.ApiKey,
                    Name = "Authorization",
                    Description = "Please copy the 'Bearer ' + valid JWT token into field",
                    In = OpenApiSecurityApiKeyLocation.Header
                }));
                config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWT Token"));
                config.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "pjx-api-dotnet";
                    document.Info.Description = "A simple ASP.NET Core web API by mikelau13";
                    document.Info.Contact = new NSwag.OpenApiContact
                    {
                        Name = "Mike Lau",
                        Email = string.Empty,
                        Url = "https://github.com/mikelau13"
                    };
                };
            });

            #region inject CalendarEvent conflict check
            services.AddDbContext<CalendarDbContext>(options => options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));
            services.AddTransient(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddCalendareEventRepository(Configuration.GetSection("CalendarEventDI"));
            #endregion

            IConfigurationRoot configurationRoot = new ConfigurationBuilder().AddEnvironmentVariables("PJX_").Build();
            IConfigurationSection section = configurationRoot.GetSection("SSO"); 
            string authAuthority = section["AUTHORITY"] ?? "http://localhost:5001"; // "https://pjx-sso-identityserver";
            Console.WriteLine("authAuthority = " + authAuthority);
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.RequireHttpsMetadata = false; // TODO: non-SSL for testing purpose and local development
                    options.Authority = authAuthority;
                    options.MetadataAddress = authAuthority + "/.well-known/openid-configuration";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false
                    };
                });

            // allows checking for the presence of the scope in the access token that the client asked for (and got granted)
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiScope", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "api1", "web_sso"); // web_sso from the pjx-web-react
                });
            });

            services.AddCors(options =>
            {
                // this defines a CORS policy called "default"
                options.AddPolicy("default", policy =>
                {
                    // allow Ajax calls to be made from https://localhost:3000 (pjx-web-react)
                    policy.WithOrigins("http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });


        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.UseRouting();
            app.UseCors("default"); // CORS middleware to the pipeline

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // setup the policy for all API endpoints in the routing system
                endpoints.MapControllers()
                    .RequireAuthorization("ApiScope");
            });
        }
    }
}
