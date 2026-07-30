# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ESTAFF is a classic ASP.NET MVC 5 web app (.NET Framework 4.8.1, not .NET Core/5+) for employee task and report management. It uses Entity Framework 6 (Code First with Migrations) against SQL Server, and ASP.NET Identity 2.x with OWIN cookie authentication. There is a single project (`ESTAFF/ESTAFF.csproj`) in the solution (`ESTAFF.slnx`).

ESTAFF shares its database with a sibling application's "CLIP" module (referred to as EHS_PORTAL) — see "Cross-schema CLIP integration" below. This is a significant architectural fact: not everything in the DB is owned by this app.

## Build & run

This is a legacy (non-SDK-style) `.csproj` targeting `net481` — there is no `dotnet` CLI support for building or running it. It is developed/run with Visual Studio or JetBrains Rider on Windows via IIS Express (`.idea/config/applicationhost.config` is checked in for Rider). If working from a shell without MSBuild available, code changes cannot be locally compiled or run — say so rather than claiming a build/test was verified.

- Package management: NuGet via `packages.config` (old-style `packages/` folder restore, not `PackageReference`).
- Database: SQL Server, connection string `DefaultConnection` in `ESTAFF/Web.config` (`Data Source=localhost; Integrated Security=True; Initial Catalog=ESH; MultipleActiveResultSets=True; TrustServerCertificate=True`). Note the catalog is `ESH`, not `ESTAFF`.
- On app start (`Global.asax.cs`), `Database.SetInitializer(new MigrateDatabaseToLatestVersion<ApplicationDbContext, Configuration>())` auto-applies pending EF migrations — there is no separate "run migrations" step; just running the app updates the schema.
- `AutomaticMigrationsEnabled = true` in `ESTAFF/Migrations/Configuration.cs`, so schema drift from model changes can be auto-migrated, but explicit migrations (`Add-Migration` from the EF6 Package Manager Console) are still the norm.
- Seed data (`Configuration.Seed`): creates a default admin user (`admin` / `Admin123`) if one doesn't exist. Note the "email" is literally the string `admin`, not a real email — this mirrors the dual login behavior described below.

There are no automated tests, lint config, or CI in this repo currently.

## Authentication & authorization model

- Auth is ASP.NET Identity 2.2.4 + OWIN cookie auth (`ESTAFF/App_Start/Startup.Auth.cs`), configured with an 8-hour sliding-expiration cookie. Lockout policy (5 failed attempts before a 5-minute lockout) is configured in `ApplicationUserManager.Create` (`ESTAFF/Models/Data/ApplicationIdentity.cs`), not in `Startup.Auth.cs`.
- `ApplicationUser` (`ESTAFF/Models/Data/ApplicationUser.cs`) extends `IdentityUser` with app-specific fields: `EmpID`, `IsAdmin`, `IsActive`, `HireDate`, `CreatedDate`, `LastModifiedDate`. There is no `FullName` field (views use `UserName`) and no `ProfilePicturePath` (removed by migration `202607270147037_removeProfilePicturePath.cs`).
- **There are no ASP.NET Identity Roles in use.** Authorization is a simple boolean flag (`IsAdmin`) on the user, not `[Authorize(Roles=...)]`. Access control is enforced by two custom `ActionFilterAttribute`s in `ESTAFF/Filters/`: `[AdminOnly]` and `[EmployeeOnly]`, applied at the controller level (see `AdminController`, `EmployeeController`). Each filter opens its own `ApplicationDbContext` to re-check `IsAdmin`/`IsActive` on every request and redirects unauthenticated/wrong-role users to `/Account/Login`. When adding a new controller, follow this same filter-per-controller convention rather than introducing Identity roles.
- Login (`AccountController.Login`) is dual-mode by design: the same credential field (`LoginViewModel.EmpID`, labeled "Employee Number" in the view) matches against **either** `Email` (how the seeded admin logs in, since its "email" is `admin`) **or** `EmpID` (how regular employees log in). Post-login redirect branches on `user.IsAdmin` to `/Admin` or `/Employee`.
- Deactivated accounts (`IsActive == false`) are blocked at login with an explicit message; the `[AdminOnly]`/`[EmployeeOnly]` filters do not re-check `IsActive` on every subsequent request, only login does.

## Data layer (EF6 Code First)

- `ApplicationDbContext` (`ESTAFF/Models/Data/ApplicationDbContext.cs`) extends `IdentityDbContext<ApplicationUser>` and exposes: `DbSet<TaskItem> TaskItems`, `DbSet<TaskHistory> TaskHistories`, `DbSet<Report> Reports`, `DbSet<ReportApproval> ReportApprovals`, `DbSet<TaskList> TaskLists`, `DbSet<TaskClassification> TaskClassifications`, plus read-only CLIP projections `DbSet<COF> COFs`, `DbSet<Plant> Plants`, `DbSet<UserPlant> UserPlants`, `DbSet<PlantMonitoring> PlantMonitoring`, `DbSet<Monitoring> Monitoring`. Identity's own tables (`AspNetUsers`, roles, etc.) come from the base class.
- **`Staff.cs` is mapped via Fluent API (`OnModelCreating`) and has a real table (`ESTAFF.Staffs`, created in migration `newInitialMigration`), but there is still no `DbSet<Staff>` property on the context** — it can't be queried via `_db.Staffs` directly. If wiring it up fully, add the `DbSet`.
- Two schemas are used deliberately: Identity tables and CLIP-owned tables (`COF`, `Plant`, `UserPlant`, `PlantMonitoring`, `Monitoring`) live in the `CLIP` schema; ESTAFF-owned tables (`Reports`, `ReportApprovals`, `Staffs`, `TaskItems`, `TaskHistories`, `TaskLists`, `TaskClassifications`) live in the `ESTAFF` schema, in the same physical database.
- **Cross-schema CLIP integration**: `COF`, `Plant`, `UserPlant`, `PlantMonitoring`, and `Monitoring` are read-only projections of tables owned/written by a sibling application's CLIP module (EHS_PORTAL), mirrored into the same DB. `ApplicationDbContext` overrides `SaveChanges`/`SaveChangesAsync` to call `PreventClipReadOnlyWrites()`, which throws if any tracked `COF`/`Plant`/`UserPlant` entity has pending changes — this is a deliberate guard against ESTAFF accidentally corrupting EHS_PORTAL's data. Never add migrations that `CreateTable`/`AddColumn` for CLIP-owned tables.
- `TaskItem` links to `TaskList` (required FK) — a `TaskList` → `TaskClassification` hierarchy — and has a nullable `COFId` linking to the CLIP `COF` projection.
- Relationships are configured via Fluent API in `OnModelCreating` (not data annotations for FKs): `TaskItem` has required `AssignedToUser`/`CreatedByUser`/`TaskList` (no cascade delete), `TaskHistory` cascades on delete from its parent `TaskItem`, `Report.User` has no cascade delete, `Staff` has required `User`/`Manager` (no cascade delete).
- **Known bug**: `TaskList`'s required relationship to `TaskClassification` is misconfigured — `ApplicationDbContext.cs`'s Fluent API wires it via `HasForeignKey(t => t.TaskListId)` (`TaskList`'s own primary key) instead of `t.TaskClassificationId` (the actual FK column defined on `TaskList`). The `[ForeignKey("TasksClassification")]` data annotation on `TaskList.TaskClassificationId` also doesn't match the real navigation property name (`TaskClassification`), so it's silently ignored. Don't copy this pattern; if touching task classification, this relationship likely needs fixing first.
- Controllers instantiate `ApplicationDbContext` directly per-controller (`private ApplicationDbContext _db = new ApplicationDbContext();`) and dispose it in an overridden `Dispose(bool)` — there is no DI container or repository abstraction. Follow this pattern for new controllers rather than introducing a service-locator or DI framework.
- `ESTAFF/Services/TaskService.cs` wraps an existing `ApplicationDbContext` (constructor-injected, not a new context): `UpdateOverdueTasks()` sweeps non-complete tasks past `DueDate` and flips them to `Overdue` (called defensively at the top of several read actions in both `AdminController.Tasks()` and multiple `EmployeeController` actions — there's no scheduled job), `LogHistory(...)` appends a `TaskHistory` row, and `GetCOF`/`GetCOFsForPlants`/`GetPlantMonitoring` back the CLIP-derived dropdowns used by `EmployeeController` and `TaskApiController`. Any task mutation that changes status/assignment/etc. should log through this service to keep the audit trail (`TaskHistory`) consistent.
- `ESTAFF/Services/ReportPdfService.cs` generates PDF reports via iTextSharp, used by `EmployeeController.DownloadReportPdf` and `AdminController.DownloadReport`.

## Controllers & views structure

- `AccountController` (auth, `[AllowAnonymous]`), `AdminController` (`[AdminOnly]`), `EmployeeController` (`[EmployeeOnly]`) are the three Razor MVC controllers. `[ValidateAntiForgeryToken]` is applied on mutating actions.
- There is also a JSON API controller: `ESTAFF/Controllers/Api/TaskApiController.cs` (`System.Web.Http.ApiController`, `[RoutePrefix("api/tasks")]`), routed via `App_Start/WebApiConfig.cs`, returning COF data for a plant.
- View models live in `ESTAFF/Models/ViewModels/` (`AccountViewModels.cs`, `StaffViewModels.cs`, `TaskViewModels.cs`, `ReportViewModels.cs`) and are distinct from the EF entities in `Models/Data/` — controllers map explicitly between them (no AutoMapper).
- Views mirror controllers 1:1 under `Views/Account`, `Views/Admin`, `Views/Employee`, each with its own layout (`Views/Shared/_AdminLayout.cshtml`, `_EmployeeLayout.cshtml`) rather than a single shared `_Layout`.
- **Both `AdminController` and `EmployeeController` are fully implemented.** `AdminController` covers dashboard stats, employee CRUD, task assignment/editing/deletion with history logging, task history audit view, and a full report review/approval workflow (`PendingReports`, `ApprovedReports`, `ReviewReport`, `ApproveReport`, `RejectReport`, `DownloadReport`). `EmployeeController` covers a dashboard, task management (`MyTasks`, `CreateTask`, `EditTask`, `DailyView`, `WeeklyView`), profile stats, and a full report submission/PDF workflow (`MyReports`, `GenerateReport`, `PreviewReport`, `SubmitReport`, `ViewReport`, `DownloadReportPdf`, `ResubmitReport`).
- Frontend: Bootstrap 5 + Font Awesome 6 + Google Fonts (Inter / Plus Jakarta Sans), all loaded via CDN `<link>` tags directly in the layout files rather than through `BundleConfig`/local `Content`/`Scripts` bundling — no npm/build step for CSS/JS.

## Conventions worth following

- Status/priority/report-type/approval fields are C# `enum`s stored as ints (`TaskStatus`, `TaskPriority`, `ReportType`, `ReportStatus`, `ApprovalStatus` in their respective model files) — extend by adding enum members, not free-text strings.
- `TaskItem.Status` values (`Pending`, `InProgress`, `Complete`, `Overdue`) are also used as filter query-string values in `AdminController.Tasks(status, employeeId)` via `Enum.TryParse` — keep enum names URL-safe.
- Timestamps use `DateTime.Now` (server local time) consistently, not `DateTime.UtcNow` — match this if adding new date fields. (One existing inconsistency: `EmployeeController.CreateTask` sets `LastModifiedDate` with `DateTime.UtcNow` — don't copy that, treat it as a bug rather than precedent.)
- "On-time rate" (`% of completed tasks where CompletedDate <= DueDate`) is computed ad hoc in multiple places (`AdminController.Index`, `Employees`, `EditEmployee`, `CalculateOnTimeRate` helper) rather than centralized — if consolidating, `TaskService` is the natural home.
- Never write to CLIP-owned entities (`COF`, `Plant`, `UserPlant`) from ESTAFF code — `ApplicationDbContext.SaveChanges` will throw. Treat them as read-only reference data owned by EHS_PORTAL.
