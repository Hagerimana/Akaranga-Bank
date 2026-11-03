using System.Globalization;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Data.Interceptors;
using AkarangaBankMvc.Web.Hubs;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Resources;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.Services.Email;
using AkarangaBankMvc.Web.Services.Export;
using AkarangaBankMvc.Web.Services.Pdf;
using AkarangaBank.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Port Railway ---
// En local, ASPNETCORE_HTTP_PORTS reste utilisé si PORT est absent.
var railwayPort = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(railwayPort))
{
    if (!int.TryParse(railwayPort, out var port)
        || port < 1
        || port > 65535)
    {
        throw new InvalidOperationException(
            "La variable PORT doit contenir un port valide.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// À activer uniquement si l'hébergeur gère le HTTPS public.
var httpsTerminatedByProxy = builder.Configuration.GetValue<bool>(
    "Hosting:HttpsTerminatedByProxy");

// --- Internationalisation ---
builder.Services.AddLocalization();

var supportedCultures = new[]
{
    new CultureInfo("fr-BI"),
    new CultureInfo("fr"),
    new CultureInfo("rn-BI"),
    new CultureInfo("rn"),
    new CultureInfo("en-US"),
    new CultureInfo("en"),
    new CultureInfo("sw-KE"),
    new CultureInfo("sw")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("fr-BI");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// --- Licence QuestPDF ---
QuestPDF.Settings.License = LicenseType.Community;

// --- Data Protection ---
// Sur Railway, définir DataProtection__KeysPath et monter un volume
// sur le même chemin pour conserver les clés entre les déploiements.
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("AkarangaBankMvc.Web");

var dataProtectionKeysPath = builder.Configuration[
    "DataProtection:KeysPath"];

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);

    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath));
}

// --- Configuration email ---
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddScoped<SmtpEmailSender>();

builder.Services.AddScoped<IAppEmailSender>(
    sp => sp.GetRequiredService<SmtpEmailSender>());

builder.Services.AddScoped<IEmailSender<ApplicationUser>>(
    sp => sp.GetRequiredService<SmtpEmailSender>());

builder.Services.AddScoped<
    ICodeVerificationService,
    CodeVerificationService>();

builder.Services.AddSingleton<
    IProfilePdfService,
    ProfilePdfService>();

// --- Base de données et audit ---
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddDbContext<AkarangaBankContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<AuditInterceptor>();

    options.UseNpgsql(
        builder.Configuration.GetConnectionString("AkarangaBank"))
        .AddInterceptors(interceptor);
});

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Comportement existant conservé.
    // Une activation nécessite un service email opérationnel.
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;

    options.Tokens.AuthenticatorTokenProvider =
        TokenOptions.DefaultAuthenticatorProvider;

    options.Tokens.AuthenticatorIssuer = "AkarangaBank";
})
.AddRoles<ApplicationRole>()
.AddEntityFrameworkStores<AkarangaBankContext>()
.AddDefaultTokenProviders();

// --- Cookie d'authentification ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Sur l'hébergement public HTTPS, le cookie reste Secure
    // même si le proxy contacte le conteneur en HTTP.
    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
        && !httpsTerminatedByProxy
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

// --- Services métier ---
builder.Services.AddScoped<IAgenceService, AgenceService>();
builder.Services.AddScoped<IClientService, ClientService>();

builder.Services.AddScoped<
    IVerificationKYCService,
    VerificationKYCService>();

builder.Services.AddScoped<ICompteService, CompteService>();

builder.Services.AddScoped<
    IUtilisateurService,
    UtilisateurService>();

builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

// --- Politiques d'autorisation ---
builder.Services.AddScoped<
    IAuthorizationHandler,
    RequireMfaHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireMfa", policy =>
        policy.Requirements.Add(new RequireMfa()));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));

    options.AddPolicy("GestionnaireOrAdmin", policy =>
        policy.RequireRole("gestionnaire", "admin"));

    options.AddPolicy("GuichetierOnly", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireRole(
            "admin",
            "gestionnaire",
            "guichetier");
    });

    options.AddPolicy("AuditeurOnly", policy =>
        policy.RequireRole("auditeur", "admin"));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- Contrôleurs MVC ---
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
})
.AddViewLocalization()
.AddDataAnnotationsLocalization(options =>
{
    options.DataAnnotationLocalizerProvider = (type, factory) =>
        factory.Create(typeof(SharedResource));
});

// --- Pages Razor Identity ---
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/Login");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/Logout");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/AccessDenied");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/LoginWith2fa");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/LoginWithRecoveryCode");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/MotDePasseOublie");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/VerifierCode");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/ReinitialiserMotDePasse");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity", "/Account/ConfirmerEmail");
})
.AddViewLocalization()
.AddDataAnnotationsLocalization(options =>
{
    options.DataAnnotationLocalizerProvider = (type, factory) =>
        factory.Create(typeof(SharedResource));
});

// --- Services métier : conformité et comptabilité ---
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    ICurrentUserService,
    CurrentUserService>();

builder.Services.AddScoped<IConformiteService, ConformiteService>();
builder.Services.AddScoped<ISurveillanceService, SurveillanceService>();
builder.Services.AddScoped<IDeclarationService, DeclarationService>();
builder.Services.AddScoped<IComptabiliteService, ComptabiliteService>();
builder.Services.AddScoped<ICompensationService, CompensationService>();

builder.Services.AddScoped<
    IVirementInterAgenceService,
    VirementInterAgenceService>();

builder.Services.AddScoped<ICNRFService, CNRFService>();
builder.Services.AddScoped<ISeuilAMLService, SeuilAMLService>();

builder.Services.AddScoped<
    IDetectionAvanceeService,
    DetectionAvanceeService>();

builder.Services.AddScoped<AlertesAvanceesService>();

// --- Services métier : prêts et cartes ---
builder.Services.AddScoped<
    ICompteInterneService,
    CompteInterneService>();

builder.Services.AddScoped<IPretService, PretService>();
builder.Services.AddScoped<ICarteService, CarteService>();

// --- Services métier : documents et notifications ---
builder.Services.AddSignalR();

builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddScoped<
    INotificationTemplateService,
    NotificationTemplateService>();

builder.Services.AddScoped<INotificationService, NotificationService>();

// --- Services métier : reporting, PDF et export ---
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IReportService, ReportService>();

// --- Vérifications de santé ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AkarangaBankContext>("database");

var app = builder.Build();

// --- Application des migrations ---
// En production, activer explicitement Database__AutoMigrate
// pour le premier déploiement si aucune migration externe n'est prévue.
var autoMigrate = builder.Configuration.GetValue(
    "Database:AutoMigrate",
    app.Environment.IsDevelopment());

if (autoMigrate)
{
    using var scopeMigration = app.Services.CreateScope();

    var contextMigration = scopeMigration.ServiceProvider
        .GetRequiredService<AkarangaBankContext>();

    if (contextMigration.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.Ordinal) == true)
    {
        await contextMigration.Database.MigrateAsync();
    }
}

// --- Initialisation des types de comptes ---
using (var scopeTypesCompte = app.Services.CreateScope())
{
    await TypeCompteInitializer.InitializeAsync(
        scopeTypesCompte.ServiceProvider);
}

// --- Initialisation des rôles et de l'administrateur ---
using (var scope = app.Services.CreateScope())
{
    await AdminInitializer.InitializeAsync(
        scope.ServiceProvider);
}

// --- Initialisation des seuils AML ---
using (var scopeSeuils = app.Services.CreateScope())
{
    await SeuilAMLInitializer.InitializeAsync(
        scopeSeuils.ServiceProvider);
}

// --- Initialisation des types de prêts ---
using (var scopeTypesPret = app.Services.CreateScope())
{
    await TypePretInitializer.InitializeAsync(
        scopeTypesPret.ServiceProvider);
}

// --- Pipeline HTTP ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    if (!httpsTerminatedByProxy)
    {
        app.UseHsts();
    }
}

// Railway termine le HTTPS au niveau de son proxy.
// Ne pas rediriger son trafic HTTP interne vers un port HTTPS inexistant.
if (!httpsTerminatedByProxy)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

var locOptions = app.Services
    .GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>()
    .Value;

app.UseRequestLocalization(locOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// --- Page d'entrée ---
app.MapGet("/", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/Home/Index");
    }

    return Results.Redirect("/Identity/Account/Login");
})
.AllowAnonymous();

// --- Routes MVC ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- Pages Razor ---
app.MapRazorPages();

// --- Notifications ---
app.MapHub<NotificationHub>("/hubs/notifications");

// --- Santé du processus ---
// Accessible sans connexion pour la sonde Railway.
// Aucun contrôle de base de données sur cette route.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
})
.AllowAnonymous();

// --- Disponibilité de la base de données ---
app.MapHealthChecks("/health/ready")
    .AllowAnonymous();

app.Run();

public partial class Program { }