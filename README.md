# LibraryMS (ASP.NET Core 8 MVC + Identity + SQLite)
Run: `dotnet restore && dotnet run`  -> open the printed URL.
Admin login: admin@lib.com / Admin@123 (seeded). Self-registered users become Members.
Roles: Admin (everything, org profile, delete books) | Librarian (inventory, issue, return) | Member (browse, own books, warnings).
Settings: Issue.LoanDays (14) and Issue.FinePerDay (5) in Models/Models.cs.
To make a Librarian: register, then set the role in the AspNetUserRoles table (or add a user-admin page).

---

**Powered by Md. Atikur Rahman Mridha**
