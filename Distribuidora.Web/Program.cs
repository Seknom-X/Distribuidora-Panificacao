using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
 o.LoginPath = "/Conta/Login"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
 o.ExpireTimeSpan = TimeSpan.FromMinutes(55); o.SlidingExpiration = false;
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireRole("Admin").Build());
builder.Services.AddHttpClient("API", c => { c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7080/"); c.Timeout = TimeSpan.FromSeconds(30); });
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("login-aula", "acesso/entrar", new { controller = "Conta", action = "Login" });
app.MapControllerRoute("default", "{controller=Painel}/{action=Index}/{id?}");
app.Run();

