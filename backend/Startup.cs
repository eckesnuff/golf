using System;
using backend.Models;
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
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1).ConfigureApiBehaviorOptions(x =>
            {
                x.SuppressModelStateInvalidFilter = true;
            });
            services.AddApplicationInsightsTelemetry();
            services.AddSingleton<Persistence>((s) =>{
                return new Persistence(
                    new Uri(Configuration["CosmosDB:URL"]),
                            Configuration["CosmosDB:PrimaryKey"]);
            });
            services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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
            //app.use
            //             app.UseEndpoints(endpoints =>
            // {
            //     endpoints.MapControllerRoute(
            //         name: "default",
            //         pattern: "{controller=Home}/{action=Index}/{id?}");
            // });
            app.UseHttpsRedirection();
            app.UseMvcWithDefaultRoute();
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
			telemetry.Context.User.Id = platformContext.User.Identity.Name;
		}
    }
}
