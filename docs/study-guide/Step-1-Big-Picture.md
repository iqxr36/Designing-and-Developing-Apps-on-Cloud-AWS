# Step 1: The Big Picture — How This App Starts and Handles Requests

Think of this project like a **restaurant**.

- **Views** = the menu and plates the customer sees (HTML pages)
- **Controllers** = waiters who take orders and decide what to do
- **Models** = the recipes and ingredients (data: users, requests, properties)
- **Program.cs** = the restaurant owner who opens the shop, hires staff, and sets the rules before anyone walks in

Your whole journey starts here: `Program.cs`.

---

## Part A: What kind of project is this?

This is an **ASP.NET Core MVC** web application.

| Word | Simple meaning |
|------|----------------|
| ASP.NET Core | Microsoft's framework for building websites in C# |
| MVC | Model–View–Controller — a way to organize code |
| Web application | A program that runs on a server and responds when someone visits a URL in a browser |

When a user opens `https://www.zammaintain.online`, their browser sends an **HTTP request**. Your server runs C# code and sends back **HTML** (a webpage).

Two main HTTP types you'll hear about:

- **GET** = "Show me a page" (like opening a menu)
- **POST** = "Here is data, please save/process it" (like submitting an order)

---

## Part B: What is Program.cs?

`Program.cs` is the **entry point** — the first file that runs when the app starts.

It does **two big jobs**:

1. **SETUP** (before the app accepts visitors)  
   → connect database, register services, configure login

2. **RUNTIME PIPELINE** (when a visitor arrives)  
   → security checks, routing, then run the right controller

Open the file and you'll see it split cleanly:

```csharp
var builder = WebApplication.CreateBuilder(args);
```

↑ Setup phase begins

```csharp
var app = builder.Build();
```

↑ Setup done. Now we configure how requests are handled.

```csharp
app.Run();
```

↑ Start the web server and keep listening forever

---

## Part C: Setup Phase — "Hiring Staff and Buying Equipment"

Everything between `CreateBuilder` and `Build()` is **registration**. You're telling the app:

> "When someone needs X, use Y."

That's called **Dependency Injection (DI)**. Beginner version:

> Instead of every class creating its own database connection or S3 client, the app **provides** them automatically.

### 1. Read configuration (`appsettings.json`)

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
```

**What this means:**  
The app reads settings from `appsettings.json` (passwords, AWS bucket name, etc.).  
`DefaultConnection` is the **database address** (AWS RDS PostgreSQL).

Think of `appsettings.json` as the **settings notebook** for the restaurant.

### 2. Register the database

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**What this means:**

- `ApplicationDbContext` = your app's **door to the database**
- `UseNpgsql` = use **PostgreSQL** (running on AWS RDS)
- Any controller that needs the database can just ask for `ApplicationDbContext` in its constructor

**Analogy:** You hire a librarian (`DbContext`) who knows how to find and store books (data) in the library (RDS).

### 3. Register AWS S3 (file storage)

```csharp
builder.Services.Configure<S3StorageOptions>(
    builder.Configuration.GetSection(S3StorageOptions.SectionName));

builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    ...
    return new AmazonS3Client(region);
});

builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
```

**What this means:**

- Photos (issue photos, proof photos, avatars) go to **Amazon S3**
- `IAmazonS3` = low-level AWS client
- `IFileStorageService` = your app's friendly wrapper (`UploadAsync`, `DownloadAsync`, `DeleteAsync`)
- Controllers don't talk to AWS directly — they use `IFileStorageService`.

### 4. Register login / users (Identity)

```csharp
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
  ...
})
.AddRoles<ApplicationRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();
```

**What this means:**

- Users log in with email/password
- Roles exist: `Tenant`, `Manager`, `Technician`, `Administrator`, `SuperAdmin`
- User accounts are stored in the **same database** via `ApplicationDbContext`
- When you're logged in, the browser holds a **cookie** (a small token). The server checks it on every request.

### 5. Register other services

```csharp
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<MessagingService>();
...
builder.Services.AddSignalR();
```

| Registration | Purpose |
|--------------|---------|
| `AddControllersWithViews()` | Enable MVC (controllers + Razor views) |
| `DashboardDataService` | Build dashboard numbers/lists |
| `MessagingService` | Chat between tenant/manager/technician |
| `AddSignalR()` | Real-time chat updates |
| `XenditPaymentService` | Payment processing |

**`AddScoped`** means: create one instance **per HTTP request**, then throw it away. That's normal for web apps.

---

## Part D: Runtime Phase — "What Happens When Someone Visits a URL"

After `var app = builder.Build()`, you configure the **middleware pipeline**.

**Middleware** = checkpoints every request passes through, in order.

```
Browser sends request
  → UseForwardedHeaders
  → UseHttpsRedirection
  → LegacyUploadsS3Middleware
  → UseStaticFiles
  → UseRouting
  → UseAuthentication
  → UseAuthorization
  → Controller action runs
  → View renders HTML
  → Response sent to browser
```

Important ones:

```csharp
app.UseHttpsRedirection();
app.UseMiddleware<LegacyUploadsS3Middleware>();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
```

| Middleware | Beginner explanation |
|------------|---------------------|
| `UseHttpsRedirection` | Force secure `https://` |
| `LegacyUploadsS3Middleware` | Old image URLs still work via S3 |
| `UseStaticFiles` | Serve CSS, JS, images from `wwwroot/` |
| `UseRouting` | Figure out which controller/action to call |
| `UseAuthentication` | Who is this user? (read login cookie) |
| `UseAuthorization` | Are they allowed? (check role) |

**Order matters.** You can't check roles before you know who the user is.

---

## Part E: Routing — "How the URL Finds the Right Code"

At the bottom of `Program.cs`:

```csharp
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

The URL is broken into pieces:

```
https://site.com / Tenant / Tenant / Dashboard
                 ↑ area   ↑ controller ↑ action
```

| URL | Area | Controller | Action | Meaning |
|-----|------|------------|--------|---------|
| `/` or `/Home/Index` | (none) | Home | Index | Landing page |
| `/Tenant/Tenant/Dashboard` | Tenant | Tenant | Dashboard | Tenant dashboard |
| `/Manager/Manager/MaintenanceRequests` | Manager | Manager | MaintenanceRequests | Manager's request list |

**Default rule:** if you don't specify, it goes to `HomeController.Index()`.

---

## Part F: MVC in Action — A Real Mini Example

### The Controller (the waiter)

```csharp
public class HomeController : Controller
{
    ...
    public IActionResult Index()
    {
        return View();
    }
}
```

When someone visits `/Home/Index`:

1. Routing picks `HomeController`
2. Runs method `Index()`
3. `return View()` means: "show the matching view file"

### The View (what the user sees)

```csharp
@{
    ViewData["Title"] = "Property Management";
    Layout = "~/Views/Shared/_LandingLayout.cshtml";
}
```

- File lives at `Views/Home/Index.cshtml` (folder matches controller name)
- `.cshtml` = **Razor** = HTML mixed with a little C#
- `Layout` = shared wrapper (header, footer, CSS)

### A link in the view

```html
<a class="landing-btn landing-btn--outline" asp-area="Identity" asp-page="/Account/RegisterCompany">Request Demo</a>
<a class="landing-btn landing-btn--primary" asp-area="Identity" asp-page="/Account/Login">Login</a>
```

`asp-controller`, `asp-action`, `asp-area` are **tag helpers**. They generate correct URLs automatically.

Clicking **Login** → browser does a **GET** to the login page (Identity area, not HomeController).

---

## Part G: The Full Picture — One Request Journey

### User opens the homepage

1. Browser: `GET https://zammaintain.online/`
2. Server: `Program.cs` pipeline runs
3. Routing: URL → `HomeController.Index`
4. `HomeController.Index()` runs
5. `return View()` → loads `Views/Home/Index.cshtml`
6. Razor engine builds HTML
7. Browser receives HTML + loads CSS from `wwwroot` (`UseStaticFiles`)
8. User sees landing page

### Logged-in Tenant opens dashboard

1. `GET /Tenant/Tenant/Dashboard`
2. `UseAuthentication` → reads cookie → knows user `john@email.com`
3. `UseAuthorization` → checks `[Authorize(Roles = "Tenant")]` on `TenantController`
4. If allowed → `TenantController.Dashboard()` runs
5. Controller loads data from database (`ApplicationDbContext`)
6. Returns a View with that data
7. HTML sent to browser

---

## Part H: Key Words to Remember for Your Presentation

| Term | One-line definition |
|------|---------------------|
| `Program.cs` | App startup: configure services + request pipeline |
| Middleware | Code that runs on every request, in order |
| Routing | URL → which controller method to run |
| Controller | C# class that handles GET/POST and decides what to do |
| View | HTML template the user sees |
| Model | Data structure (class) representing a database table |
| DbContext | Bridge between C# code and PostgreSQL database |
| Dependency Injection | App automatically provides services controllers need |
| GET | Read/show something |
| POST | Submit/create/update something |
| `[Authorize]` | Only logged-in users with the right role can access |

---

## Part I: Mental Model — Store This in Your Head

```
┌─────────────────────────────────────────────────────┐
│                    Program.cs                        │
│  1. Register database, S3, login, services          │
│  2. Build request pipeline                          │
│  3. Map URLs to controllers                         │
│  4. app.Run() — start listening                     │
└─────────────────────────────────────────────────────┘
                          │
          When user clicks or visits URL
                          ▼
┌─────────────────────────────────────────────────────┐
│              Middleware pipeline                     │
│  HTTPS → static files → routing → auth → controller │
└─────────────────────────────────────────────────────┘
                          ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  Controller  │───▶│   Services   │───▶│  Database    │
│  (logic)     │    │  (S3, email) │    │  (RDS)       │
└──────────────┘    └──────────────┘    └──────────────┘
        │
        ▼
┌──────────────┐
│    View      │  → HTML back to browser
│  (.cshtml)   │
└──────────────┘
```

---

## Part J: What You Should Be Able to Say After Step 1

Practice saying this out loud:

> "When the app starts, `Program.cs` registers the database connection to AWS RDS, S3 file storage, user login, and business services. When a user visits a URL, the request passes through middleware for HTTPS, static files, routing, and authentication. Routing maps the URL to a Controller action. The controller runs C# logic, often using the database or S3, then returns a Razor View which becomes HTML in the browser."

### Common lecturer questions

**Q: Where does the app connect to AWS?**  
**A:** In `Program.cs` during startup — PostgreSQL via the connection string, and S3 via `AmazonS3Client` and `S3FileStorageService`.

**Q: What's the difference between frontend and backend here?**  
**A:** Views in `.cshtml` are frontend (HTML). Controllers, Services, and DbContext are backend (C# logic and data).

---

## Homework before Step 2

1. Open `Program.cs` and find the two lines: `CreateBuilder` and `Build()`.
2. Find `AddDbContext` — that's your database.
3. Find `AddScoped<IFileStorageService` — that's S3.
4. Find `MapControllerRoute` — that's URL routing.
5. Open `HomeController.cs` and `Views/Home/Index.cshtml` — see how they connect.

When ready, continue with **Step 2: AWS connections (RDS + S3)**.
