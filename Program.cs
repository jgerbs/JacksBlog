// Jack Gerber - A01266976
// Production Program.cs (Resend Email, Render-ready)

using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Resend;
using Microsoft.Extensions.Options;
using BlogApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// DATABASE + IDENTITY
// ------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
});

// ------------------------------------------------------------
// RESEND EMAIL SERVICE
// ------------------------------------------------------------

// Load secret from environment variable (Render)
builder.Services.Configure<ResendClientOptions>(opt =>
{
    opt.ApiToken = builder.Configuration["RESEND_API_KEY"]
        ?? Environment.GetEnvironmentVariable("RESEND_API_KEY")
        ?? throw new Exception("Missing RESEND_API_KEY");
});

builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
builder.Services.AddHttpClient<ResendClient>();


// ------------------------------------------------------------
// MVC
// ------------------------------------------------------------
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ------------------------------------------------------------
// AUTO MIGRATION + ADMIN SEEDING
// ------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services    = scope.ServiceProvider;
    var context     = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    await context.Database.MigrateAsync();

    // Create required roles
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    if (!await roleManager.RoleExistsAsync("Contributor"))
        await roleManager.CreateAsync(new IdentityRole("Contributor"));

    // Admin credentials from env variables
    var adminEmail    = Environment.GetEnvironmentVariable("ADMIN_EMAIL")    ?? "admin@demo.com";
    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "ChangeThis123!";

    var admin = await userManager.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "User",
            EmailConfirmed = true,
            IsApproved = true
        };

        var created = await userManager.CreateAsync(admin, adminPassword);
        if (created.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
    else
    {
        // Ensure they always stay admin
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}

// ------------------------------------------------------------
// MIDDLEWARE
// ------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Required for Render
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

// ------------------------------------------------------------
// ROUTING
// ------------------------------------------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
