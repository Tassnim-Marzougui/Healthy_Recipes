using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Healthy_Recipes.Data;
using Healthy_Recipes.Models;
using Healthy_Recipes.Services;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services au conteneur.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configurer le contexte de la base de données
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurer ASP.NET Core Identity
builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Configurer le pipeline de requêtes HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Initialisation de la base de données
using (var scope = app.Services.CreateScope())
{
    await SeedService.SeedDatabase(scope.ServiceProvider);
}

app.Run();