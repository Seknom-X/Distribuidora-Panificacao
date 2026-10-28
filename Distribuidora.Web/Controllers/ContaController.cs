using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Distribuidora.Web.Controllers;
public sealed class ContaController(IHttpClientFactory factory) : Controller {
 [AllowAnonymous, HttpGet] public IActionResult Login() => View();
 [AllowAnonymous, HttpPost] public async Task<IActionResult> Login(string email, string senha) {
  try {
   using var r = await factory.CreateClient("API").PostAsJsonAsync("api/auth/login", new { email, senha });
   if (!r.IsSuccessStatusCode) { ViewBag.Erro = "Credenciais inválidas ou acesso indisponível."; return View(); }
   using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync()); var root = j.RootElement; var u = root.GetProperty("usuario");
   if (u.GetProperty("perfil").GetString() != "Admin") { ViewBag.Erro = "Este painel exige perfil Admin."; return View(); }
   await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim(ClaimTypes.Name, u.GetProperty("nome").GetString() ?? "Admin"), new Claim(ClaimTypes.Role, "Admin"),
    new Claim("access_token", root.GetProperty("token").GetString()!) }, CookieAuthenticationDefaults.AuthenticationScheme)),
    new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(55), IsPersistent = false });
   return RedirectToAction("Index", "Painel");
  } catch (HttpRequestException) { ViewBag.Erro = "API indisponível. Confira endereço e certificado local."; return View(); }
    catch (TaskCanceledException) { ViewBag.Erro = "Tempo de conexão esgotado."; return View(); }
 }
 [HttpPost] public async Task<IActionResult> Sair() { await HttpContext.SignOutAsync(); return RedirectToAction(nameof(Login)); }
}
