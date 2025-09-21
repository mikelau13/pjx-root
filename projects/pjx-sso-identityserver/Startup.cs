// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using IdentityServer4.Configuration;
using IdentityServerAspNetIdentity.Data;
using IdentityServerAspNetIdentity.Models;
using IdentityServerAspNetIdentity.Services;
using IdentityServerAspNetIdentity.Services.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityServerAspNetIdentity
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddSwaggerDocument(config =>
            {
                config.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "pjx-sso-identityserver";
                    document.Info.Description = "IdentityServer4 for pjx project by mikelau13";
                    document.Info.Contact = new NSwag.OpenApiContact
                    {
                        Name = "Mike Lau",
                        Email = string.Empty,
                        Url = "https://github.com/mikelau13"
                    };
                };
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                options.EmitStaticAudienceClaim = true;
                options.Authentication = new AuthenticationOptions()
                {
                    CookieLifetime = System.TimeSpan.FromMinutes(5), // TODO: easier to do testing by setting timeout to 5 minutes, will need to change back to hours for production
                    CookieSlidingExpiration = true
                };
            })
                .AddInMemoryIdentityResources(Config.IdentityResources)
                .AddInMemoryApiScopes(Config.ApiScopes)
                .AddInMemoryClients(Config.Clients)
                .AddAspNetIdentity<ApplicationUser>();

            //// not recommended for production - you need to store your key material somewhere secure
            // builder.AddDeveloperSigningCredential();

            // self-signed certificate generate with https://github.com/mikelau13/pjx-api-dotnet/tree/master/src/Pjx_CreateCretificates
            // test it using jwts endpoint /.well-known/openid-configuration/jwks
            IConfigurationRoot configurationRoot = new ConfigurationBuilder().AddEnvironmentVariables("PJX_").Build();
            IConfigurationSection section = configurationRoot.GetSection("SSO"); 
            string certFile = section["CERTIFICATE"] ?? "pjx-sso-identityserver.rsa_2048.cert.pfx";
            string certPassword = section["PASSWORD"] ?? "password";
            var rsaCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(System.IO.Path.Combine(Environment.ContentRootPath, certFile), certPassword);
            builder.AddSigningCredential(rsaCertificate);

            services.AddCors(options =>
                    {
                        options.AddPolicy("CorsPolicy",
                            builder => builder.WithOrigins("http://localhost:3000")
                            .AllowAnyMethod()
                            .AllowAnyHeader());
                    });
            // uncomment to enable Google+ API
            //services.AddAuthentication()
            //    .AddGoogle(options =>
            //    {
            //        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

            //        // register your IdentityServer with Google at https://console.developers.google.com
            //        // enable the Google+ API
            //        // set the redirect URI to https://localhost:5001/signin-google
            //        options.ClientId = "copy client ID from Google here";
            //        options.ClientSecret = "copy client secret from Google here";
            //    });

            // Secrets manager
            SecretsManager secrets = new SecretsManager
            {
                SmtpPassword = Configuration["Pjx:SmtpPassword"]
            };
            services.AddSingleton<SecretsManager>(secrets);

            // Email service
            services.Configure<EmailConfig>(Configuration.GetSection("Email"));
            services.AddTransient<IEmailService, EmailService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }

            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.UseStaticFiles();
            app.UseCors("CorsPolicy");
            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}