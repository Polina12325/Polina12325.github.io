using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using KovaneIzdeliya.Data;
using KovaneIzdeliya.Models;
using KovaneIzdeliya.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка для DateTime с PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Подключение к PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Добавляем MVC и Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Регистрация сервисов (DI)
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Для работы сессий (корзина)
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Инициализация ролей и администратора
await SeedRolesAndAdmin(app);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();           // ВАЖНО: сессии до аутентификации
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

// -------------------------------------------------------
// Seed: создаём роли Admin/User и первого администратора
// -------------------------------------------------------
static async Task SeedRolesAndAdmin(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Создаём роли, если не существуют
    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Назначаем роль User всем пользователям без роли
    foreach (var user in userManager.Users.ToList())
    {
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any())
            await userManager.AddToRoleAsync(user, "User");
    }

    // Создаём администратора по умолчанию, если ни одного нет
    var adminEmail = "admin@kovane.ru";
    var adminPassword = "Admin123";

    if ((await userManager.GetUsersInRoleAsync("Admin")).Count == 0)
    {
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Администратор",
                CreatedAt = DateTime.Now,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
        else
        {
            await userManager.AddToRoleAsync(existingAdmin, "Admin");
        }
    }
}