using SmartHome.Domain.Interfaces.Devices;
using SmartHome.Domain.Interfaces.Users;
using SmartHome.Domain.Interfaces.Rooms;
using SmartHome.Domain.Interfaces.MaintenanceLogs;
using SmartHome.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SmartHome.Infrastructure.Repositories;
using Serilog;
using SmartHome.Infrastructure.Services;
using SmartHome.Api.BackgroundServices;
using SmartHome.Api.Hubs;
using SmartHome.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Logger Configuration ---
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
);

// --- 2. Services Configuration (DI Container) ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Database Provider Registration ---
if (builder.Environment.EnvironmentName != "Testing")
{
    if (builder.Environment.IsDevelopment())
    {
        // LOCAL: Use SQLite
        var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection")
                         ?? "Data Source=smarthome.db";
        builder.Services.AddDbContext<SmartHomeDbContext>(options =>
            options.UseSqlite(sqliteConn));

        Console.WriteLine("Registered SQLite for Development.");
    }
    else
    {
        // PRODUCTION: Use SQL Server
        var sqlServerConn = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<SmartHomeDbContext>(options =>
            options.UseSqlServer(sqlServerConn));

        Console.WriteLine("Registered SQL Server for Production.");
    }

    // Register Background Services for non-testing environments
    builder.Services.AddHostedService<TcpSmartHomeServer>();
    builder.Services.AddHostedService<MqttListenerService>();
}

// Authentication Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SmartHomeAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;

        if (builder.Environment.IsEnvironment("Testing"))
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        }
        else
        {
            // Production (cross-site HTTPS)
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        }

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.AddSignalR();
builder.Services.AddScoped<IDeviceNotifier, SignalRNotifier>();

// Repository and Service Registration
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMaintenanceLogRepository, MaintenanceLogRepository>();
builder.Services.AddScoped<IMaintenanceLogService, MaintenanceLogService>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- 3. Database Initialization (Execute Migrations/Creation) ---
if (app.Environment.EnvironmentName != "Testing")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartHomeDbContext>();
        try
        {
            // Check if the actual model tables exist (not just __EFMigrationsHistory)
            bool modelTablesExist;
            try
            {
                // If this query succeeds, the Users table exists
                _ = db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users'")
                    .ToList();
                modelTablesExist = db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users'")
                    .Single() > 0;
            }
            catch
            {
                modelTablesExist = false;
            }

            if (!modelTablesExist)
            {
                // Remove stale __EFMigrationsHistory (left by prior failed deployments)
                // so EnsureCreated() doesn't skip table creation
                try
                {
                    db.Database.ExecuteSqlRaw(
                        "IF OBJECT_ID('__EFMigrationsHistory') IS NOT NULL DROP TABLE [__EFMigrationsHistory]");
                }
                catch { /* ignore if it doesn't exist */ }

                db.Database.EnsureCreated();
                Console.WriteLine($"Database tables created ({app.Environment.EnvironmentName}).");
            }
            else
            {
                Console.WriteLine($"Database is ready ({app.Environment.EnvironmentName}).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization failed: {ex.Message}");
        }
    }
}

// --- 4. Pipeline Section (Middleware) ---
app.UseCors("AllowReactApp");
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SmartHomeHub>("/smarthomehub");

app.Run();