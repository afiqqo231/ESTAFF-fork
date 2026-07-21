# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ESTAFF is a classic ASP.NET MVC 5 web app (.NET Framework 4.8.1, not .NET Core/5+) for employee task and report management. It uses Entity Framework 6 (Database-First-style Code First with Migrations) against SQL Server, and ASP.NET Identity 2.x with OWIN cookie authentication. There is a single project (`ESTAFF/ESTAFF.csproj`) in the solution (`ESTAFF.slnx`).

## Build & run

This is a legacy (non-SDK-style) `.csproj` targeting `net481` — there is no `dotnet` CLI support for building or running it. It is developed/run with Visual Studio or JetBrains Rider on Windows via IIS Express (`.idea/config/applicationhost.config` is checked in for Rider). If working from a shell without MSBuild available, code changes cannot be locally compiled or run — say so rather than claiming a build/test was verified.

- Package management: NuGet via `packages.config` (old-style `packages/` folder restore, not `PackageReference`).
- Database: SQL Server, connection string `DefaultConnection` in `ESTAFF/Web.config` (`Data Source=localhost; Initial Catalog=ESTAFF; Integrated Security=True`).
- On app start (`Global.asax.cs`), `Database.SetInitializer(new MigrateDatabaseToLatestVersion<ApplicationDbContext, Configuration>())` auto-applies pending EF migrations — there is no separate "run migrations" step; just running the app updates the schema.
- `AutomaticMigrationsEnabled = true` in `ESTAFF/Migrations/Configuration.cs`, so schema drift from model changes can be auto-migrated, but explicit migrations (`Add-Migration` from the EF6 Package Manager Console) are still the norm — see `ESTAFF/Migrations/202607090259382_InitialCreate.cs` for the pattern.
- Seed data (`Configuration.Seed`): creates a default admin user (`admin` / `Admin123`) if one doesn't exist. Note the "email" is literally the string `admin`, not a real email — this mirrors the dual login behavior described below.

There are no automated tests, lint config, or CI in this repo currently.

## Authentication & authorization model

- Auth is ASP.NET Identity 2.2.4 + OWIN cookie auth (`ESTAFF/App_Start/Startup.Auth.cs`), configured with an 8-hour sliding-expiration cookie, 5 failed attempts before a 5-minute lockout.
- `ApplicationUser` (`ESTAFF/Models/Data/ApplicationUser.cs`) extends `IdentityUser` with app-specific fields: `FullName`, `EmpNumber`, `IsAdmin`, `IsActive`, `ProfilePicturePath`, `HireDate`.
- **There are no ASP.NET Identity Roles in use.** Authorization is a simple boolean flag (`IsAdmin`) on the user, not `[Authorize(Roles=...)]`. Access control is enforced by two custom `ActionFilterAttribute`s in `ESTAFF/Filters/`: `[AdminOnly]` and `[EmployeeOnly]`, applied at the controller level (see `AdminController`, `EmployeeController`). Each filter opens its own `ApplicationDbContext` to re-check `IsAdmin`/`IsActive` on every request and redirects unauthenticated/wrong-role users to `/Account/Login`. When adding a new controller, follow this same filter-per-controller convention rather than introducing Identity roles.
- Login (`AccountController.Login`) is dual-mode by design: the same credential field (labeled "Employee Number" in the view/`LoginViewModel.EmpNumber`) matches against **either** `Email` (how the seeded admin logs in, since its "email" is `admin`) **or** `EmpNumber` (how regular employees log in). Post-login redirect branches on `user.IsAdmin` to `/Admin` or `/Employee`.
- Deactivated accounts (`IsActive == false`) are blocked at login with an explicit message; the `[AdminOnly]`/`[EmployeeOnly]` filters do not re-check `IsActive` on every subsequent request, only login does.

## Data layer (EF6 Code First)

- `ApplicationDbContext` (`ESTAFF/Models/Data/ApplicationDbContext.cs`) extends `IdentityDbContext<ApplicationUser>` and exposes `DbSet<TaskItem> Tasks`, `DbSet<TaskHistory> TaskHistories`, `DbSet<Report> Reports`. Identity's own tables (`AspNetUsers`, roles, etc.) come from the base class.
- **`Staff.cs` and `ReportApproval.cs` exist as model classes in `Models/Data/` but are not registered as `DbSet`s on the context and have no corresponding migration.** They are effectively dead/unfinished code — do not assume they are backed by real tables; if wiring them up, add both the `DbSet` and a migration.
- Relationships are configured via Fluent API in `OnModelCreating` (not data annotations for FKs): `TaskItem` has required `AssignedToUser`/`CreatedByUser` (no cascade delete), `TaskHistory` cascades on delete from its parent `TaskItem`, `Report.User` has no cascade delete.
- Controllers instantiate `ApplicationDbContext` directly per-controller (`private ApplicationDbContext _db = new ApplicationDbContext();`) and dispose it in an overridden `Dispose(bool)` — there is no DI container or repository abstraction. Follow this pattern for new controllers rather than introducing a service-locator or DI framework.
- `ESTAFF/Services/TaskService.cs` is the one extracted piece of business logic, wrapping an existing `ApplicationDbContext` (constructor-injected, not a new context): `UpdateOverdueTasks()` sweeps non-complete tasks past `DueDate` and flips them to `Overdue` (called defensively at the top of `AdminController.Tasks()` on every page load — there's no scheduled job), and `LogHistory(...)` appends a `TaskHistory` row. Any task mutation in `AdminController` that changes status/assignment/etc. should log through this service to keep the audit trail (`TaskHistory`) consistent.

## Controllers & views structure

- Three controllers, one per concern: `AccountController` (auth, `[AllowAnonymous]`), `AdminController` (`[AdminOnly]`), `EmployeeController` (`[EmployeeOnly]`). No API/JSON controllers — everything is server-rendered Razor MVC with `ViewBag`/typed view models, form posts, and `[ValidateAntiForgeryToken]` on mutating actions.
- View models live in `ESTAFF/Models/ViewModels/` (`AccountViewModels.cs`, `StaffViewModels.cs`, `TaskViewModels.cs`) and are distinct from the EF entities in `Models/Data/` — controllers map explicitly between them (no AutoMapper).
- Views mirror controllers 1:1 under `Views/Account`, `Views/Admin`, `Views/Employee`, each with its own layout (`Views/Shared/_AdminLayout.cshtml`, `_EmployeeLayout.cshtml`) rather than a single shared `_Layout`.
- **`AdminController` is the fully-implemented side** (dashboard stats, employee CRUD, task assignment/editing/deletion with history logging, task history audit view). **`EmployeeController` is currently view-only scaffolding** — every action (`MyTasks`, `CreateTask`, `DailyView`, `WeeklyView`, `MyReports`, `GenerateReport`, `Profile`) just sets `ViewBag` title/subtitle and returns an empty view with no data/DB logic wired up yet. `PendingReports`/`ApprovedReports` on `AdminController` are similarly stubbed (return `View()` with no query). The `Report`/`ReportApproval` approval workflow described by the models is not implemented in any controller yet.
- Frontend: Bootstrap 5 + Font Awesome 6 + Google Fonts (Inter / Plus Jakarta Sans), all loaded via CDN `<link>` tags directly in the layout files rather than through `BundleConfig`/local `Content`/`Scripts` bundling — no npm/build step for CSS/JS.

## Conventions worth following

- Status/priority/report-type/approval fields are C# `enum`s stored as ints (`TaskStatus`, `TaskPriority`, `ReportType`, `ReportStatus`, `ApprovalStatus` in their respective model files) — extend by adding enum members, not free-text strings.
- `TaskItem.Status` values (`Pending`, `InProgress`, `Complete`, `Overdue`) are also used as filter query-string values in `AdminController.Tasks(status, employeeId)` via `Enum.TryParse` — keep enum names URL-safe.
- Timestamps use `DateTime.Now` (server local time) consistently, not `DateTime.UtcNow` — match this if adding new date fields.
- "On-time rate" (`% of completed tasks where CompletedDate <= DueDate`) is computed ad hoc in multiple places (`AdminController.Index`, `Employees`, `EditEmployee`, `CalculateOnTimeRate` helper) rather than centralized — if consolidating, `TaskService` is the natural home.
