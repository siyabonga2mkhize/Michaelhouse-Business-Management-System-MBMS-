using Michaelhouse.Infrastructure;
using Michaelhouse.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllersWithViews();

// EF Core
builder.Services.AddDbContext<DBContextClass>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MichaelHouse")));

// Session (used for role-based navigation instead of ASP.NET Core Identity)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// HttpContextAccessor (needed by services that resolve IConfiguration via DI)
builder.Services.AddHttpContextAccessor();

// Application services
builder.Services.AddScoped<Michaelhouse.Services.EmailService>();
builder.Services.AddScoped<Michaelhouse.Services.AiReviewService>();
builder.Services.AddScoped<Michaelhouse.Services.ApplicationService>();
builder.Services.AddScoped<Michaelhouse.Services.PaymentService>();
builder.Services.AddScoped<Michaelhouse.Services.AIResidenceAllocationService>();
builder.Services.AddScoped<Michaelhouse.Services.AIResidencePredictionService>();
builder.Services.AddScoped<Michaelhouse.Services.AIQRCodeVerificationService>();
builder.Services.AddScoped<Michaelhouse.Services.ResidenceAllocationEngine>();
builder.Services.AddScoped<Michaelhouse.Services.ResidenceAvailabilityService>();

// ── Stripe ────────────────────────────────────────────────────────────────────
var stripeKey = builder.Configuration["StripeSecretKey"];
if (!string.IsNullOrWhiteSpace(stripeKey))
    StripeConfiguration.ApiKey = stripeKey;

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

// ── Routes ────────────────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Configure static infrastructure helpers
using (var scope = app.Services.CreateScope())
{
    var dbContextOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<DBContextClass>>();
    DbContextFactory.Configure(dbContextOptions);
}
AppConfig.Configure(app.Configuration);
PathHelper.Configure(app.Environment.ContentRootPath);

app.Run();
