using ChatterBox.Data;
using ChatterBox.Hubs;
using ChatterBox.Models;
using ChatterBox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    if (builder.Environment.IsDevelopment())
    {
        serverOptions.ListenLocalhost(5094);
        serverOptions.ListenLocalhost(7264, listenOptions => listenOptions.UseHttps());
    }
    else
    {
        serverOptions.ListenAnyIP(8080);
    }
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure Identity - UPDATED with Role support
builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add Services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<AdminInitializationService>();

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdminOnly", policy => policy.RequireRole("SystemAdmin"));
});

// Configure Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.Cookie.Name = "ChatterBox.Cookie";
});

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure SignalR
builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.EnableDetailedErrors = true;
    hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(15);
    hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    hubOptions.HandshakeTimeout = TimeSpan.FromSeconds(15);
    hubOptions.MaximumReceiveMessageSize = 102400;
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed((host) => true)
            .AllowCredentials());
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// Global error handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An unhandled exception occurred.");

        if (!context.Response.HasStarted)
        {
            context.Request.Path = "/Home/Error";
            await next();
        }
    }
});

app.UseStaticFiles();
app.UseRouting();
app.UseCors("CorsPolicy");

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints with retry logic
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Configure SignalR endpoints with options
app.MapHub<ChatHub>("/chatHub", options =>
{
    options.ApplicationMaxBufferSize = 102400;
    options.TransportMaxBufferSize = 102400;
    options.WebSockets.CloseTimeout = TimeSpan.FromSeconds(5);
});

app.MapHub<NotificationHub>("/notificationHub");

try
{
    // Initialize database and create admin
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            // Ensure database is created and migrations are applied
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();

            // Initialize admin role and user
            var adminInitService = services.GetRequiredService<AdminInitializationService>();
            await adminInitService.InitializeAsync();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during database migration or admin initialization.");
        }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Application startup failed");
    throw;
}