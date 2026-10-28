using SQLite;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
namespace Distribuidora.Offline;
public sealed class CacheEntry { [PrimaryKey] public string Chave {get;set;}=""; public string Json {get;set;}="[]"; public DateTime AtualizadoUtc {get;set;} }
public sealed class PedidoLocal {
 [PrimaryKey] public string Chave {get;set;}="";
 public string Corpo {get;set;}="";
 public string Estado {get;set;}="Pendente";
 public string Erro {get;set;}="";
 public int? PedidoServidorId {get;set;}
}
public sealed record ItemPedido(int ProdutoId,int QuantidadeSolicitada);
public sealed class LojaOffline : IDisposable {
 private readonly SQLiteConnection db;
 private readonly HttpClient api;
 private readonly SemaphoreSlim gate=new(1,1);
 public LojaOffline(string path,HttpClient client) {
  SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
  db=new SQLiteConnection(path); db.CreateTable<CacheEntry>(); db.CreateTable<PedidoLocal>(); api=client;
 }
 public string? Token { set => api.DefaultRequestHeaders.Authorization = value is null ? null : new AuthenticationHeaderValue("Bearer",value); }
 public async Task<(string Token,string Nome)> Login(string email,string senha,CancellationToken ct=default) {
  using var r=await api.PostAsJsonAsync("api/auth/login",new {email,senha},ct); r.EnsureSuccessStatusCode();
  using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); var root=j.RootElement;
  if(root.GetProperty("usuario").GetProperty("perfil").GetString()!="Vendedor") throw new InvalidOperationException("Use uma conta Vendedor.");
  var token=root.GetProperty("token").GetString()!; Token=token;
  return (token,root.GetProperty("usuario").GetProperty("nome").GetString()!);
 }
 public async Task AtualizarCache(CancellationToken ct=default) {
  await gate.WaitAsync(ct);
  try {
   var entries=new List<CacheEntry>();
   foreach(var nome in new[]{"clientes","produtos","pedidos"}) {
    using var r=await api.GetAsync("api/me/"+nome,ct); r.EnsureSuccessStatusCode();
    var json=await r.Content.ReadAsStringAsync(ct); using var parsed=JsonDocument.Parse(json);
    if(parsed.RootElement.ValueKind!=JsonValueKind.Array) throw new JsonException();
    entries.Add(new CacheEntry {Chave=nome,Json=json,AtualizadoUtc=DateTime.UtcNow});
   }
   db.RunInTransaction(()=>{foreach(var e in entries) db.InsertOrReplace(e);});
  } finally {gate.Release();}
 }
 public CacheEntry? Cache(string nome)=>db.Find<CacheEntry>(nome);
 public List<PedidoLocal> Pedidos()=>db.Table<PedidoLocal>().ToList();
 public PedidoLocal CriarPedido(int clienteId,IReadOnlyList<ItemPedido> itens) {
  if(clienteId<=0 || itens.Count is <1 or >100 || itens.Any(i=>i.ProdutoId<=0 || i.QuantidadeSolicitada is <1 or >1000000) || itens.Select(i=>i.ProdutoId).Distinct().Count()!=itens.Count) throw new ArgumentException("Cliente, itens ou quantidades inválidos.");
  var clientes=JsonDocument.Parse(Cache("clientes")?.Json??"[]"); var produtos=JsonDocument.Parse(Cache("produtos")?.Json??"[]");
  using(clientes) using(produtos) {
   if(!clientes.RootElement.EnumerateArray().Any(c=>c.GetProperty("id").GetInt32()==clienteId) || itens.Any(i=>!produtos.RootElement.EnumerateArray().Any(p=>p.GetProperty("id").GetInt32()==i.ProdutoId))) throw new ArgumentException("Atualize a carteira e o catálogo antes de cadastrar.");
  }
  var chave=Guid.NewGuid().ToString("D");
  var pedido=new PedidoLocal {Chave=chave,Corpo=JsonSerializer.Serialize(new {chaveRequisicao=chave,clienteId,itens=itens.Select(i=>new {produtoId=i.ProdutoId,quantidadeSolicitada=i.QuantidadeSolicitada})})};
  db.Insert(pedido); return pedido;
 }
 public async Task Sincronizar(CancellationToken ct=default) {
  await gate.WaitAsync(ct);
  try {
   foreach(var p in Pedidos().Where(p=>p.Estado=="Pendente")) {
    try {
     using var r=await api.PostAsync("api/me/pedidos",new StringContent(p.Corpo,Encoding.UTF8,"application/json"),ct);
     if(r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new UnauthorizedAccessException("Entre novamente para sincronizar.");
     if(r.IsSuccessStatusCode) {
      using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); p.PedidoServidorId=j.RootElement.GetProperty("id").GetInt32(); p.Estado="Sincronizado"; p.Erro="";
     } else {
      p.Erro=$"HTTP {(int)r.StatusCode}: "+await r.Content.ReadAsStringAsync(ct);
      if((int)r.StatusCode>=400 && (int)r.StatusCode<500 && r.StatusCode is not HttpStatusCode.RequestTimeout && (int)r.StatusCode!=429) p.Estado="Revisar";
     }
     db.Update(p);
    } catch(HttpRequestException) { p.Erro="Sem conexão. O pedido será reenviado com o mesmo identificador."; db.Update(p); break; }
      catch(TaskCanceledException) when(!ct.IsCancellationRequested) { p.Erro="Tempo esgotado; reenvio seguro pendente."; db.Update(p); break; }
   }
  } finally {gate.Release();}
 }
 public void Dispose() {db.Dispose(); gate.Dispose();}
}
