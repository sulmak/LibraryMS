using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryMS.Models;
namespace LibraryMS.Controllers;

public class AccountController(UserManager<AppUser> um, SignInManager<AppUser> sm) : Controller
{
    public IActionResult Login() => View();
    [HttpPost] public async Task<IActionResult> Login(string email, string password, bool remember)
    {
        var u = await um.FindByEmailAsync(email);
        if (u != null && (await sm.PasswordSignInAsync(u, password, remember, false)).Succeeded) return RedirectToAction("Index", "Home");
        ViewBag.Error = "Invalid email or password"; return View();
    }
    public IActionResult Register() => View();
    [HttpPost] public async Task<IActionResult> Register(string fullName, string email, string password)
    {
        var u = new AppUser { UserName = email, Email = email, FullName = fullName };
        var r = await um.CreateAsync(u, password);
        if (!r.Succeeded) { ViewBag.Error = string.Join(", ", r.Errors.Select(e => e.Description)); return View(); }
        await um.AddToRoleAsync(u, "Member"); await sm.SignInAsync(u, false); return RedirectToAction("Index", "Home");
    }
    [HttpPost] public async Task<IActionResult> Logout() { await sm.SignOutAsync(); return RedirectToAction(nameof(Login)); }
    public IActionResult Denied() => View();
}

[Authorize]
public class HomeController(AppDb db, UserManager<AppUser> um) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Member"))
            ViewBag.Mine = await db.Issues.Include(i => i.Book).Where(i => i.UserId == um.GetUserId(User) && i.ReturnedOn == null).ToListAsync();
        else
        {
            var open = await db.Issues.Where(i => i.ReturnedOn == null).ToListAsync();
            ViewBag.Titles = await db.Books.CountAsync(); ViewBag.Available = await db.Books.SumAsync(b => b.Available);
            ViewBag.Issued = open.Count; ViewBag.Overdue = open.Count(i => i.DaysLate > 0);
            ViewBag.Members = (await um.GetUsersInRoleAsync("Member")).Count;
            ViewBag.Fines = (await db.Issues.Where(i => i.ReturnedOn != null).Select(i => i.Fine).ToListAsync()).Sum() + open.Sum(i => i.CurrentFine);
        }
        return View();
    }
}

[Authorize]
public class BooksController(AppDb db) : Controller
{
    public async Task<IActionResult> Index(string? q, bool onlyAvailable)
    {
        var s = db.Books.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) s = s.Where(b => b.Title.Contains(q) || b.Author.Contains(q) || b.ISBN!.Contains(q));
        if (onlyAvailable) s = s.Where(b => b.Available > 0);
        return View(await s.OrderBy(b => b.Title).ToListAsync());
    }
    [Authorize(Roles = "Admin,Librarian")] public IActionResult Create() => View();
    [Authorize(Roles = "Admin,Librarian"), HttpPost] public async Task<IActionResult> Create(Book b)
    { b.Available = b.TotalCopies; db.Add(b); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [Authorize(Roles = "Admin"), HttpPost] public async Task<IActionResult> Delete(int id)
    { var b = await db.Books.FindAsync(id); if (b != null && !db.Issues.Any(i => i.BookId == id && i.ReturnedOn == null)) { db.Remove(b); await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
}

[Authorize]
public class CirculationController(AppDb db, UserManager<AppUser> um) : Controller
{
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> Issue()
    { ViewBag.Users = await um.GetUsersInRoleAsync("Member"); ViewBag.Books = await db.Books.Where(b => b.Available > 0).ToListAsync(); return View(); }
    [Authorize(Roles = "Admin,Librarian"), HttpPost]
    public async Task<IActionResult> Issue(string userId, int bookId)
    {
        var b = await db.Books.FindAsync(bookId);
        if (b is { Available: > 0 })
        {
            b.Available--; db.Add(new Models.Issue { BookId = bookId, UserId = userId, IssuedOn = DateTime.Today, DueOn = DateTime.Today.AddDays(Models.Issue.LoanDays) });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Active));
    }
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> Active() => View(await db.Issues.Include(i => i.Book).Include(i => i.User).Where(i => i.ReturnedOn == null).OrderBy(i => i.DueOn).ToListAsync());
    [Authorize(Roles = "Admin,Librarian"), HttpPost]
    public async Task<IActionResult> Return(int id)
    {
        var i = await db.Issues.Include(x => x.Book).FirstOrDefaultAsync(x => x.Id == id && x.ReturnedOn == null);
        if (i != null) { i.Fine = i.CurrentFine; i.ReturnedOn = DateTime.Today; i.Book!.Available++; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Active));
    }
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> History() => View(await db.Issues.Include(i => i.Book).Include(i => i.User).Where(i => i.ReturnedOn != null).OrderByDescending(i => i.ReturnedOn).Take(200).ToListAsync());
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> MyBooks() => View(await db.Issues.Include(i => i.Book).Where(i => i.UserId == um.GetUserId(User)).OrderByDescending(i => i.IssuedOn).ToListAsync());
}

[Authorize]
public class ProfileController(AppDb db, UserManager<AppUser> um, IWebHostEnvironment env) : Controller
{
    static readonly string[] Ok = { ".jpg", ".jpeg", ".png", ".webp" };
    async Task<string?> Save(IFormFile? f)
    {
        if (f == null || f.Length == 0 || f.Length > 2_000_000 || !Ok.Contains(Path.GetExtension(f.FileName).ToLower())) return null;
        var dir = Path.Combine(env.WebRootPath, "uploads"); Directory.CreateDirectory(dir);
        var n = Guid.NewGuid() + Path.GetExtension(f.FileName).ToLower();
        using var fs = System.IO.File.Create(Path.Combine(dir, n)); await f.CopyToAsync(fs); return "/uploads/" + n;
    }
    public async Task<IActionResult> Me() => View(await um.GetUserAsync(User));
    [HttpPost] public async Task<IActionResult> Me(string fullName, IFormFile? photo)
    { var u = (await um.GetUserAsync(User))!; u.FullName = fullName; u.PhotoPath = await Save(photo) ?? u.PhotoPath; await um.UpdateAsync(u); return RedirectToAction(nameof(Me)); }
    [Authorize(Roles = "Admin")] public async Task<IActionResult> Org() => View(await db.Org.FirstAsync());
    [Authorize(Roles = "Admin"), HttpPost] public async Task<IActionResult> Org(string name, string? address, string? phone, IFormFile? logo)
    { var o = await db.Org.FirstAsync(); o.Name = name; o.Address = address; o.Phone = phone; o.LogoPath = await Save(logo) ?? o.LogoPath; await db.SaveChangesAsync(); return RedirectToAction(nameof(Org)); }
}
