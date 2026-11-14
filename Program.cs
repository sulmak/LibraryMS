using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LibraryMS.Models;
var b = WebApplication.CreateBuilder(args);
b.Services.AddDbContext<AppDb>(o => o.UseSqlite("Data Source=library.db"));
b.Services.AddIdentity<AppUser, IdentityRole>(o => { o.Password.RequireNonAlphanumeric = false; })
    .AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
b.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Denied"; });
b.Services.AddControllersWithViews();
var app = b.Build();
using (var s = app.Services.CreateScope())
{
    var db = s.ServiceProvider.GetRequiredService<AppDb>(); db.Database.EnsureCreated();
    var rm = s.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var um = s.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    foreach (var r in new[] { "Admin", "Librarian", "Member" }) if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole(r));
    if (await um.FindByEmailAsync("admin@lib.com") == null)
    {
        var a = new AppUser { UserName = "admin@lib.com", Email = "admin@lib.com", FullName = "Administrator" };
        await um.CreateAsync(a, "Admin@123"); await um.AddToRoleAsync(a, "Admin");
    }
    if (!db.Org.Any()) { db.Org.Add(new OrgProfile { Name = "My Library" }); db.SaveChanges(); }
}
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();
