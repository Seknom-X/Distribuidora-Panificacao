using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Distribuidora.Web.Models;
using Microsoft.AspNetCore.Mvc;
namespace Distribuidora.Web.Controllers;
public sealed class PainelController(IHttpClientFactory factory) : Controller {
 private HttpClient Api() { var c = factory.CreateClient("API"); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value); return c; }
 public async Task<IActionResult> Index() {
 ViewBag.Resumo=await Enviar("api/admin/painel/resumo","GET",null);
 return View();
}
[HttpPost] public async Task<IActionResult> Desativar(string chave,int id) {
 if(id<=0 || !Operacoes.Todas.TryGetValue(chave??"",out var op) || op.Metodo!="CRUD") return BadRequest();
 var dados=await Enviar(op.Rota+$"/{id}/desativar","PATCH",new JsonObject());
 return View("Modulo",new TelaPainel(chave!,op,dados,new()));
}
 public async Task<IActionResult> Modulo(string chave, int? editar) {
  if (!Operacoes.Todas.TryGetValue(chave ?? "",out var op)) return NotFound();
  var valores = new Dictionary<string,string>(); JsonElement? dados = null;
  if (!op.Rota.Contains('{')) {
   dados = await Enviar(op.Rota + (editar is > 0 && op.Metodo == "CRUD" ? $"/{editar}" : ""), "GET", null);
   if (editar is > 0 && dados is { ValueKind: JsonValueKind.Object }) { foreach(var p in dados.Value.EnumerateObject()) valores[p.Name] = p.Value.ToString(); valores["editar"] = editar.ToString()!; }
  }
  foreach(var f in op.Campos.Where(f => f.Tipo == "uuid")) valores[f.Nome] = Guid.NewGuid().ToString("D");
  return View(new TelaPainel(chave!,op,dados,valores));
 }
 [HttpPost] public async Task<IActionResult> Executar(string chave, string? acao) {
  if (!Operacoes.Todas.TryGetValue(chave ?? "",out var op)) return NotFound();
  var form = await Request.ReadFormAsync(); var valores = form.ToDictionary(x=>x.Key,x=>x.Value.ToString());
  var rota = op.Rota; var body = new JsonObject();
  try {
   foreach(var f in op.Campos) {
    var v = form[f.Nome].ToString();
    if (rota.Contains("{"+f.Nome+"}")) { if (!int.TryParse(v,out var n) || n <= 0) throw new FormatException(); rota = rota.Replace("{"+f.Nome+"}",n.ToString()); continue; }
    body[f.Nome] = f.Tipo switch {
     "number" => JsonValue.Create(int.Parse(v)), "decimal" => JsonValue.Create(decimal.Parse(v.Replace(',','.'),System.Globalization.CultureInfo.InvariantCulture)),
     "ids" => new JsonArray(v.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(n => (JsonNode?)JsonValue.Create(int.Parse(n.Trim()))).ToArray()),
     "itens" or "parcelas" => Linhas(form,f), _ => JsonValue.Create(v)
    };
   }
   var metodo = op.Metodo == "CRUD" ? "POST" : op.Metodo;
   if (op.Metodo == "CRUD" && int.TryParse(form["editar"],out var id) && id > 0) { rota += $"/{id}"; metodo = "PUT"; }
   if (acao == "desativar" && op.Metodo == "CRUD" && int.TryParse(form["registro"],out var registro) && registro > 0) { rota = op.Rota+$"/{registro}/desativar"; metodo="PATCH"; }
   var dados = await Enviar(rota,metodo,metodo=="GET" ? null : body);
   valores.Remove("senha");
   return View("Modulo",new TelaPainel(chave!,op,dados,valores));
  } catch (Exception ex) when (ex is FormatException or OverflowException or JsonException) { ViewBag.Erro = "Confira os números e os campos obrigatórios."; valores.Remove("senha"); return View("Modulo",new TelaPainel(chave!,op,null,valores)); }
 }
 private static JsonArray Linhas(IFormCollection form, Campo f) {
  var a = new JsonArray(); var left = form[f.Nome+"A"]; var right = form[f.Nome+"B"];
  for(var i=0;i<left.Count;i++) { if(string.IsNullOrWhiteSpace(left[i])) continue;
   a.Add(f.Tipo=="itens" ? new JsonObject { ["produtoId"] = int.Parse(left[i]!), ["quantidadeSolicitada"] = int.Parse(right[i]!) } : new JsonObject { ["diasAposVenda"] = int.Parse(left[i]!), ["percentual"] = decimal.Parse(right[i]!.Replace(',','.'),System.Globalization.CultureInfo.InvariantCulture) }); }
  return a;
 }
 private async Task<JsonElement?> Enviar(string rota,string metodo,JsonObject? body) {
  try {
   using var req = new HttpRequestMessage(new HttpMethod(metodo),rota);
   if(body!=null) req.Content=JsonContent.Create(body);
   using var c=Api(); using var r=await c.SendAsync(req);
   var text=await r.Content.ReadAsStringAsync();
   if(!r.IsSuccessStatusCode) ViewBag.Erro = $"Operação não concluída (HTTP {(int)r.StatusCode}). " + (r.StatusCode is System.Net.HttpStatusCode.Unauthorized ? "Entre novamente." : text);
   else ViewBag.Sucesso="Consulta/operação concluída.";
   return string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
  } catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or JsonException) { ViewBag.Erro="Não foi possível obter a resposta da API. Em operações de gravação, consulte o resultado antes de tentar novamente; preserve o identificador."; return null; }
 }
}



