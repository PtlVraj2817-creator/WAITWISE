using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaitWise.Data;
using WaitWise.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Database Connection (MySQL via Pomelo)
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var csBuilder = new MySqlConnector.MySqlConnectionStringBuilder(rawConnectionString);

// Read password securely from user configuration / secrets / env if provided
var dbPassword = builder.Configuration["DbPassword"] 
    ?? builder.Configuration["Database:Password"]
    ?? builder.Configuration["MySQL:Password"]
    ?? builder.Configuration["DB_PASSWORD"];

if (!string.IsNullOrEmpty(dbPassword))
{
    csBuilder.Password = dbPassword;
}

var dbUser = builder.Configuration["DbUser"] 
    ?? builder.Configuration["Database:User"]
    ?? builder.Configuration["MySQL:User"]
    ?? builder.Configuration["DB_USER"];

if (!string.IsNullOrEmpty(dbUser))
{
    csBuilder.UserID = dbUser;
}

var connectionString = csBuilder.ConnectionString;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 2. Configure ASP.NET Core Identity for Authentication & Roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Simplified password settings for smooth development and testing
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// 3. Register Application Services (Predictor & Queue Management)
builder.Services.AddScoped<WaitWise.Services.IQueuePredictorService, WaitWise.Services.QueuePredictorService>();

// 4. Add SignalR for live queue updates
builder.Services.AddSignalR();

// 5. Add MVC & Razor Views
builder.Services.AddControllersWithViews();



var app = builder.Build();

// Automatically seed roles, demo admin/staff users, departments, counters, and baseline service logs
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure HTTP request pipeline

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<WaitWise.Hubs.QueueHub>("/hubs/queue");

app.Run();


