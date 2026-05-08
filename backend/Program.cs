using System;
using backend;
using backend.Models;
using backend.Services;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<Persistence>(_ => new Persistence(
    new Uri(builder.Configuration["CosmosDB:URL"]),
    builder.Configuration["CosmosDB:PrimaryKey"]));
builder.Services.AddSingleton<MyGolfService>();
builder.Services.AddSingleton<ClaudeService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
builder.Services.AddControllersWithViews().AddNewtonsoftJson();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var persistence = scope.ServiceProvider.GetRequiredService<Persistence>();
    await persistence.EnsureSetupAsync();
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();

namespace backend
{
    public class CustomTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Initialize(ITelemetry telemetry)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.User?.Identity?.IsAuthenticated != true)
                return;
            telemetry.Context.User.AuthenticatedUserId = ctx.User.Identity.Name;
        }
    }
}
