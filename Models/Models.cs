using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
namespace LibraryMS.Models;
public class AppUser : IdentityUser { public string FullName { get; set; } = ""; public string? PhotoPath { get; set; } }
public class Book { public int Id { get; set; } public string Title { get; set; } = ""; public string Author { get; set; } = "";
    public string? ISBN { get; set; } public string? Category { get; set; } public int TotalCopies { get; set; } = 1; public int Available { get; set; } = 1; }
public class OrgProfile { public int Id { get; set; } public string Name { get; set; } = ""; public string? Address { get; set; } public string? Phone { get; set; } public string? LogoPath { get; set; } }
public class Issue
{
    public const decimal FinePerDay = 5; public const int LoanDays = 14;
    public int Id { get; set; } public int BookId { get; set; } public Book? Book { get; set; }
    public string UserId { get; set; } = ""; public AppUser? User { get; set; }
    public DateTime IssuedOn { get; set; } public DateTime DueOn { get; set; } public DateTime? ReturnedOn { get; set; } public decimal Fine { get; set; }
    [NotMapped] public int DaysLate => Math.Max(0, ((ReturnedOn ?? DateTime.Today) - DueOn).Days);
    [NotMapped] public decimal CurrentFine => DaysLate * FinePerDay;
    [NotMapped] public int DaysLeft => (DueOn - DateTime.Today).Days;
}
public class AppDb(DbContextOptions<AppDb> o) : IdentityDbContext<AppUser>(o)
{ public DbSet<Book> Books => Set<Book>(); public DbSet<Issue> Issues => Set<Issue>(); public DbSet<OrgProfile> Org => Set<OrgProfile>(); }
