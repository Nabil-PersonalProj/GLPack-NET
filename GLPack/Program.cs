using GLPack.DAL;
using GLPack.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// MVC with global [Authorize] by default
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// EF Core + services
string? defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(defaultConnection));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(defaultConnection),
    ServiceLifetime.Scoped);

builder.Services.AddScoped<ITransactionsService, TransactionsService>();
builder.Services.AddScoped<IAccountsService, AccountsService>();
builder.Services.AddScoped<ICompaniesService, CompaniesService>();
builder.Services.AddScoped<IAppLogger, AppLogger>();
builder.Services.AddScoped<ISqlScriptService, SqlScriptService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<ILedgerSearchService, LedgerSearchService>();
builder.Services.AddScoped<ITransactionImportService, TransactionImportService>();

// Cookie auth
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/UserAccount/Login";
        options.LogoutPath = "/UserAccount/Logout";
        options.AccessDeniedPath = "/UserAccount/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

// Admin policy using IsAdmin claim
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",
        policy => policy.RequireClaim("IsAdmin", "True"));
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    ISqlScriptService sqlScriptService = scope.ServiceProvider.GetRequiredService<ISqlScriptService>();
    await sqlScriptService.ApplyPendingScriptsAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        IAppLogger logger = context.RequestServices.GetRequiredService<IAppLogger>();

        await logger.LogAsync(
            eventType: "ERROR",
            level: "ERROR",
            logCode: "UNHANDLED_EXCEPTION",
            logMessage: $"Unhandled exception on {context.Request.Method} {context.Request.Path}: {ex.Message}",
            sourceFile: nameof(Program),
            sourceFunction: "RequestPipeline",
            ct: context.RequestAborted);

        throw;
    }
});

app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
