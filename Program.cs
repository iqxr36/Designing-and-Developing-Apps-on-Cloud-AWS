using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Options;
using CloudMVCApplication.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;



var builder = WebApplication.CreateBuilder(args);

// Database connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// PostgreSQL connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));
builder.Services.PostConfigure<AppSettings>(options =>
{
    options.BaseUrl = options.BaseUrl.Trim().TrimEnd('/');
});

builder.Services.Configure<XenditSettings>(builder.Configuration.GetSection(XenditSettings.SectionName));
builder.Services.PostConfigure<XenditSettings>(options =>
{
    options.ApiKey = options.ApiKey.Trim();
    options.CallbackToken = options.CallbackToken.Trim();
    options.Currency = string.IsNullOrWhiteSpace(options.Currency) ? "MYR" : options.Currency.Trim().ToUpperInvariant();
    options.SuccessRedirectBaseUrl = options.SuccessRedirectBaseUrl.Trim().TrimEnd('/');
    options.FailureRedirectBaseUrl = options.FailureRedirectBaseUrl.Trim().TrimEnd('/');
    options.WebhookBaseUrl = options.WebhookBaseUrl.Trim().TrimEnd('/');
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<S3StorageOptions>(
    builder.Configuration.GetSection(S3StorageOptions.SectionName));

builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<S3StorageOptions>>()
        .Value;

    if (string.IsNullOrWhiteSpace(options.Region))
    {
        throw new InvalidOperationException(
            "S3Storage:Region is not configured.");
    }

    var region = RegionEndpoint.GetBySystemName(options.Region);

    return new AmazonS3Client(region);
});

builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
builder.Services.AddScoped<IAvatarService, AvatarService>();

// Identity setup
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
.AddRoles<ApplicationRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<MessagingService>();
builder.Services.AddScoped<PlatformAdminService>();
builder.Services.AddScoped<CompanyBillingHistoryService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IXenditPaymentService, XenditPaymentService>();
builder.Services.AddScoped<XenditSubscriptionService>();
builder.Services.AddScoped<TechnicianPayoutService>();
builder.Services.AddScoped<TechnicianPaymentService>();
builder.Services.AddScoped<TechnicianWorkloadService>();
builder.Services.AddScoped<SubscriptionLimitService>();
builder.Services.AddSignalR();

var app = builder.Build();

try
{
    await LocalUploadPathMigration.MigrateAsync(
        app.Services,
        app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(
        ex,
        "Local upload path migration could not run. Paths still using /uploads/ will keep working via wwwroot until this succeeds.");
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    await DbSeeder.SeedDevelopmentDataAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<LegacyUploadsS3Middleware>();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Area route: /Admin/Dashboard/Index, /Tenant/MyRequests/Index, etc.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Default route: /Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.MapHub<CloudMVCApplication.Hubs.ChatHub>("/hubs/chat");

app.Run();

public partial class Program { }
