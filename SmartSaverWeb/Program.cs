using OfficeOpenXml;
using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels; // this is where PaynlesDbContext was scaffolded
using Paynles.Services;               // at the top
var builder = WebApplication.CreateBuilder(args);
// ============================
// BEGIN: Swagger services
// ============================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ============================
// END: Swagger services
// ============================

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<YourNamespace.Services.PaapiClient>(); // <-- add this line

// This registers services for both API and View controllers
builder.Services.AddTransient<Paynles.Services.ProductFileImporter>();
builder.Services.AddTransient<ProductFileImporter>();  // in the service registrations
builder.Services.AddHttpClient<SmartSaverWeb.Services.KeepaClient>(client =>
{
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
});
// ============================
// Pricing / Keepa services
// ============================
builder.Services.AddScoped<SmartSaverWeb.Services.Pricing.Keepa.KeepaPriceUpdateService>();
builder.Services.AddRazorPages();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
// Add CORS services
builder.Services.AddCors();
// EF Core DbContext registration
builder.Services.AddDbContext<paynles_dbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);

    // Session cookie settings (IMPORTANT for persistence)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // REQUIRED so browser sends cookie on refresh/navigation
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Required if using HTTPS (you are)
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});



// ============================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
// ============================
// Swagger (DEV ONLY)
// ============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
// Use CORS Policy
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.UseAuthorization();
app.MapControllers(); // For API controllers

// 🔁 Backward compatibility redirect for Chrome extension
app.MapGet("/MyProducts", (HttpContext ctx) =>
{
    var email = ctx.Request.Query["email"].ToString();

    var target = string.IsNullOrWhiteSpace(email)
        ? "/TrackedProducts"
        : $"/TrackedProducts?email={Uri.EscapeDataString(email)}";

    return Results.Redirect(target, permanent: true);
});
// Route /TrackedProducts to the existing Home/Index React host
app.MapControllerRoute(
    name: "trackedproducts",
    pattern: "TrackedProducts",
    defaults: new { controller = "Home", action = "Index" }
);
app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});
// Map default controller route for the home page
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
