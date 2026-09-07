# ZamMaintain

ZamMaintain is an ASP.NET Core MVC property maintenance platform for managing property companies, users, units, maintenance requests, technician assignment, messaging, notifications, completion proof, and technician payment workflows.

## System Summary

The application supports a full maintenance workflow:

- Companies manage properties, units, managers, tenants, and reports.
- Tenants submit and track maintenance requests.
- Managers review requests, assign technicians, monitor workload, and confirm work progress.
- Technicians view assigned jobs, update job status, upload completion proof, and track earnings.
- Platform administrators manage companies, technician approvals, payouts, commission reporting, and platform audit logs.

## Tech Stack

- ASP.NET Core MVC / Razor Pages
- .NET 8
- Entity Framework Core
- ASP.NET Core Identity
- PostgreSQL via Npgsql
- SignalR for chat/messaging
- Bootstrap, custom CSS, and JavaScript

## Roles

- SuperAdmin: Platform-level administrator for company, technician, payout, commission, and audit oversight.
- Administrator: Company administrator who manages users, properties, units, reports, maintenance, settings, and payments.
- Manager: Company manager who reviews requests, assigns technicians, tracks repairs, messaging, reports, and payments.
- Tenant: Resident/user who submits requests, tracks status, messages staff, and gives feedback.
- Technician: Service provider who receives jobs, updates statuses, uploads proof, and views earnings.

## Login URLs

Standard company users:

```text
/Identity/Account/Login
```

Platform Super Admin:

```text
/SuperAdmin/Login
```

SuperAdmin users must use `/SuperAdmin/Login`. The normal login page blocks SuperAdmin sign-in and tells the user to use the platform login.

## Seeded Demo Accounts

These accounts are created by `Data/DbSeeder.cs` when the app runs in `Development` and seeding is enabled.

| Role | Name | Email | Password | Login URL |
| --- | --- | --- | --- | --- |
| SuperAdmin | Platform Super Admin | `platform@zammaintain.test` | `Platform12345` | `/SuperAdmin/Login` |
| Administrator | Rania Aziz | `admin@login.test` | `abdullah100` | `/Identity/Account/Login` |
| Administrator | Amina Rahman | `admin@cedarheights.test` | `abdullah100` | `/Identity/Account/Login` |
| Manager | Farid Hakim | `manager@login.test` | `abdullah100` | `/Identity/Account/Login` |
| Manager | Marcus Tan | `admin@test.com` | `Admin12345` | `/Identity/Account/Login` |
| Manager | Omar Khalid | `manager@cedarheights.test` | `abdullah100` | `/Identity/Account/Login` |
| Tenant | Aisha Karim | `tenant@login.test` | `abdullah100` | `/Identity/Account/Login` |
| Tenant | Maya Chen | `maya.chen@cedarheights.test` | `abdullah100` | `/Identity/Account/Login` |
| Tenant | Daniel Brooks | `daniel.brooks@cedarheights.test` | `abdullah100` | `/Identity/Account/Login` |
| Technician | Daniel Wong | `technician@login.test` | `abdullah100` | `/Identity/Account/Login` |
| Technician | Luis Rivera | `luis.rivera@zammaintain.test` | `abdullah100` | `/Identity/Account/Login` |
| Technician | Priya Nair | `priya.nair@zammaintain.test` | `abdullah100` | `/Identity/Account/Login` |
| Pending Technician | Jordan Ellis | `pending.tech@zammaintain.test` | `abdullah100` | `/Identity/Account/Login` |

## Requirements

Install:

- .NET 8 SDK
- PostgreSQL 18 or compatible PostgreSQL server
- Visual Studio 2022 or another .NET-capable editor

Optional for AWS deployment:

- AWS CLI
- AWS Toolkit for Visual Studio
- Amazon RDS PostgreSQL
- EC2 Windows Server with IIS

## Local Setup

1. Restore dependencies:

```powershell
dotnet restore
```

2. Configure the database connection string.

For local PostgreSQL, set `ConnectionStrings:DefaultConnection` in `appsettings.json` or use an environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=PropertyMaintenanceDb;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

3. Run the app:

```powershell
dotnet run
```

4. Open the local URL shown in the terminal.

## Database And Seeding

The app reads:

```text
ConnectionStrings:DefaultConnection
```

from configuration.

In `Development`, `Program.cs` calls:

```csharp
await DbSeeder.SeedDevelopmentDataAsync(app.Services);
```

The seeder applies migrations through:

```csharp
await context.Database.MigrateAsync();
```

That means running the app in `Development` creates/updates the database schema and inserts seed data.

To reset an RDS PostgreSQL database before reseeding, run this in DBeaver or another SQL client connected to the target database:

```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
```

Then run the app again with the RDS connection string.

## AWS/RDS Configuration

Use the RDS endpoint as the host:

```text
Host=YOUR_RDS_ENDPOINT;Port=5432;Database=postgres;Username=postgres;Password=YOUR_RDS_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

For deployment, do not store RDS credentials in `appsettings.json`. Set this environment variable on the server:

```text
ConnectionStrings__DefaultConnection
```

RDS security group rules should allow PostgreSQL port `5432` from:

- Your local IP for local testing.
- The EC2 security group for production app traffic.

Avoid opening RDS to `0.0.0.0/0`.

## Common Commands

Build:

```powershell
dotnet build
```

Run:

```powershell
dotnet run
```

Apply migrations manually:

```powershell
dotnet ef database update
```

NuGet Package Manager Console alternative:

```powershell
Update-Database
```

In this project, `dotnet run` in `Development` already applies migrations and seeds data.

## Project Structure

```text
Areas/
  Administrator/    Company admin portal
  Identity/         Login, registration, password pages
  Manager/          Manager portal
  SuperAdmin/       Platform admin portal
  Technician/       Technician portal
  Tenant/           Tenant portal
Controllers/        Public/root MVC controllers
Data/               EF DbContext and seed data
Hubs/               SignalR hubs
Migrations/         EF Core migrations
Models/             Domain and Identity models
Services/           Business services
ViewModels/         View-specific models
Views/              Shared/root MVC views
wwwroot/            Static assets, CSS, JS, images
Program.cs          App startup and routing
appsettings.json    Non-secret app configuration
```

## Deployment Notes

Before deployment:

- Remove real passwords and API keys from `appsettings.json`.
- Use environment variables or AWS Secrets Manager for secrets.
- Confirm RDS security group allows traffic from the EC2 security group.
- Publish the app using Visual Studio or `dotnet publish`.
- Configure IIS with the ASP.NET Core Hosting Bundle.
- Set `ASPNETCORE_ENVIRONMENT` intentionally. Production should normally be `Production`.

## Security Notes

- Do not commit AWS access keys, RDS passwords, Xendit keys, or other secrets.
- Rotate any secret that was shared in screenshots or chat.
- Keep `.gitignore` excluding generated and local files such as `bin/`, `obj/`, `_build_out/`, `verify-build/`, `.vscode/`, and `*.user`.

## Useful Pages

```text
/                       Home page
/Identity/Account/Login Standard user login
/SuperAdmin/Login       Platform admin login
/Home/Privacy           Privacy policy
/Home/Terms             Terms of service
```

