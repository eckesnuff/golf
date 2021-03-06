﻿using System;
using backend.Models;
using backend.Services;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddSingleton<Persistence>((s) =>{
                return new Persistence(
                    new Uri(Configuration["CosmosDB:URL"]),
                            Configuration["CosmosDB:PrimaryKey"]);
            });
            services.AddSingleton<GitCredentials>((s)=>{
                return
                new GitCredentials{
                    UserName=Configuration["GIT:UserName"],
                    Password=Configuration["GIT:Password"],
                    OrgId=Configuration["GIT:OrgId"]
                };
            });
            services.AddSingleton<MyGolfService>();
            
            services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
            services.AddControllersWithViews().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseEndpoints(x=>x.MapControllerRoute(name: "default",
         pattern: "{controller=Home}/{action=Index}/{id?}"));
            //app.UseMvcWithDefaultRoute();
        }
    }
    public class CustomTelemetryInitializer : TelemetryInitializerBase {
		public CustomTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
			 : base(httpContextAccessor)
		{
		}
		protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
		{
			if (platformContext?.User?.Identity.IsAuthenticated != true)
			{
				return;
			}
			// telemetry.Context.User.Id = platformContext.User.Identity.Name;
            telemetry.Context.User.AuthenticatedUserId=platformContext.User.Identity.Name;
		}
    }
}
