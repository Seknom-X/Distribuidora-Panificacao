using System.Net;
using System.Text;
using System.Text.Json;
using Distribuidora.Offline;
namespace Distribuidora.Tests;
public class OfflineTests {
 sealed class FakeApi:HttpMessageHandler {
  public List<string> Corpos=[];public bool Falhar;public HttpStatusCode Status=HttpStatusCode.Created;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) {
   if(r.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent(r.RequestUri!.AbsolutePath.EndsWith("clientes") ? "[{\"id\":1}]" : "[{\"id\":2}]")};
   Corpos.Add(await r.Content!.ReadAsStringAsync(ct)); if(Falhar)throw new HttpRequestException();
   return new(Status){Content=new StringContent("{\"id\":42}",Encoding.UTF8,"application/json")};
  }
 }
 [Fact] public async Task ReenvioMantemCorpoEChaveAposFalhaEReabertura() {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db");var fake=new FakeApi();using var http=new HttpClient(fake){BaseAddress=new Uri("https://example.test/")};
  try {
   string chave;
   using(var local=new LojaOffline(path,http)){await local.AtualizarCache();var p=local.CriarPedido(1,[new(2,3)]);chave=p.Chave;fake.Falhar=true;await local.Sincronizar();Assert.Equal("Pendente",local.Pedidos().Single().Estado);}
   using(var local=new LojaOffline(path,http)){fake.Falhar=false;await local.Sincronizar();Assert.Equal(chave,local.Pedidos().Single().Chave);Assert.Equal(42,local.Pedidos().Single().PedidoServidorId);await local.Sincronizar();}
   Assert.Equal(2,fake.Corpos.Count);Assert.Equal(fake.Corpos[0],fake.Corpos[1]);
  }finally{File.Delete(path);}
 }
 [Fact] public async Task ConflitoNaoEntraEmLoopNemDescartaPedido() {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db");var fake=new FakeApi{Status=HttpStatusCode.Conflict};using var http=new HttpClient(fake){BaseAddress=new Uri("https://example.test/")};
  try{using var local=new LojaOffline(path,http);await local.AtualizarCache();local.CriarPedido(1,[new(2,1)]);await local.Sincronizar();await local.Sincronizar();Assert.Equal("Revisar",local.Pedidos().Single().Estado);Assert.Single(fake.Corpos);}finally{File.Delete(path);}
 }
 [Fact] public async Task PedidoRejeitaProdutoDuplicadoEClienteForaDaCarteira() {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db");using var http=new HttpClient(new FakeApi()){BaseAddress=new Uri("https://example.test/")};
  try{using var local=new LojaOffline(path,http);await local.AtualizarCache();Assert.Throws<ArgumentException>(()=>local.CriarPedido(1,[new(2,1),new(2,2)]));Assert.Throws<ArgumentException>(()=>local.CriarPedido(99,[new(2,1)]));Assert.Empty(local.Pedidos());}finally{File.Delete(path);}
 }
}
