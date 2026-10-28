using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Distribuidora.Offline;
namespace Distribuidora.App;
public sealed class MainPage : ContentPage {
 readonly Entry url=new(){Placeholder="API HTTPS (certificado confiável)"};
 readonly Entry email=new(){Placeholder="E-mail",Keyboard=Keyboard.Email};
 readonly Entry senha=new(){Placeholder="Senha",IsPassword=true};
 readonly Label status=new(){Text="Entre para carregar a carteira. Depois, use o cache offline."};
 readonly VerticalStackLayout root=new(){Padding=20,Spacing=12};
 LojaOffline? loja; HttpClient? client; bool busy;
 readonly List<ItemPedido> carrinho=[];
 public MainPage() {
  Title="Distribuidora · Vendedor";
  url.Text=Preferences.Default.Get("api","");
  root.Add(new Label{Text="Distribuidora de Panificação",FontSize=24}); root.Add(url); root.Add(email); root.Add(senha);
  Botao("Entrar",Entrar); Botao("Continuar offline",Offline); root.Add(status); Content=new ScrollView{Content=root};
 }
 void Botao(string text,Func<Task> action) { var b=new Button{Text=text}; b.Clicked+=async(_,_)=>{
  if(busy)return; busy=true; b.IsEnabled=false;
  try{await action();}catch(Exception ex){status.Text=ex is HttpRequestException ? "Conexão indisponível. Confira a rede, a URL e o certificado." : ex.Message;}finally{busy=false;b.IsEnabled=true;}
 }; root.Add(b); }
 void Abrir(string baseUrl,string key) {
  loja?.Dispose(); client?.Dispose();
  client=new HttpClient{BaseAddress=new Uri(baseUrl),Timeout=TimeSpan.FromSeconds(25)};
  loja=new LojaOffline(Path.Combine(FileSystem.AppDataDirectory,key+".db"),client);
 }
 async Task Entrar() {
  if(!Uri.TryCreate(url.Text?.Trim().TrimEnd('/')+"/",UriKind.Absolute,out var uri)||uri.Scheme!="https") throw new ArgumentException("Informe uma URL HTTPS válida.");
  var account=email.Text?.Trim().ToLowerInvariant()??"";
  var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri+"|"+account)));
  Abrir(uri.AbsoluteUri,key);
  var login=await loja!.Login(account,senha.Text??""); senha.Text="";
  await SecureStorage.Default.SetAsync("token",login.Token);
  await SecureStorage.Default.SetAsync("cache",key);
  Preferences.Default.Set("api",uri.AbsoluteUri);
  Mostrar(); status.Text="Conectado. Atualize a carteira antes de trabalhar offline.";
 }
 async Task Offline() {
  var token=await SecureStorage.Default.GetAsync("token"); var key=await SecureStorage.Default.GetAsync("cache"); var api=Preferences.Default.Get("api","");
  if(token is null||key is null||string.IsNullOrWhiteSpace(api))throw new InvalidOperationException("Faça o primeiro login online neste aparelho.");
  Abrir(api,key); loja!.Token=token; Mostrar(); status.Text="Cache offline. A sincronização exige sessão válida e conexão.";
 }
 void Mostrar() {
  root.Clear(); root.Add(status);
  Botao("Atualizar carteira e catálogo",async()=>{await loja!.AtualizarCache();status.Text="Cache atualizado em "+DateTime.Now.ToString("g");});
  Botao("Roteiro de visitas",Roteiro);
  var cliente=new Picker{Title="Cliente"};var produto=new Picker{Title="Produto"};var quantidade=new Entry{Placeholder="Quantidade",Keyboard=Keyboard.Numeric};
  List<JsonElement> clientes=[]; List<JsonElement> produtos=[];
  void Carregar(){clientes=Lista("clientes");produtos=Lista("produtos");cliente.ItemsSource=clientes.Select(c=>c.GetProperty("razaoSocial").GetString()).ToList();produto.ItemsSource=produtos.Select(p=>$"{p.GetProperty("nome")} · R$ {p.GetProperty("precoVenda")}").ToList();}
  Botao("Carregar seletores do cache",()=>{Carregar();return Task.CompletedTask;});
  root.Add(cliente);root.Add(produto);root.Add(quantidade);var resumo=new Label();root.Add(resumo);
  Botao("Adicionar produto",()=>{
   if(produto.SelectedIndex<0||!int.TryParse(quantidade.Text,out var q)||q is <1 or >1000000)throw new ArgumentException("Selecione produto e quantidade válida.");
   var id=produtos[produto.SelectedIndex].GetProperty("id").GetInt32(); if(carrinho.Any(i=>i.ProdutoId==id))throw new ArgumentException("Produto já adicionado.");
   carrinho.Add(new ItemPedido(id,q));resumo.Text=string.Join("\n",carrinho.Select(i=>$"Produto {i.ProdutoId}: {i.QuantidadeSolicitada}"));return Task.CompletedTask;
  });
  Botao("Limpar itens",()=>{carrinho.Clear();resumo.Text="";return Task.CompletedTask;});
  Botao("Salvar pedido offline",()=>{
   if(cliente.SelectedIndex<0)throw new ArgumentException("Selecione o cliente.");
   var p=loja!.CriarPedido(clientes[cliente.SelectedIndex].GetProperty("id").GetInt32(),carrinho.ToArray());
   carrinho.Clear();resumo.Text="";status.Text="Pedido salvo neste aparelho: "+p.Chave;return Task.CompletedTask;
  });
  Botao("Sincronizar pedidos",async()=>{await loja!.Sincronizar();status.Text=string.Join("\n",loja.Pedidos().Select(p=>$"{p.Chave[..8]}: {p.Estado} {p.Erro}"));});
  Botao("Pedidos locais",()=>{status.Text=string.Join("\n",loja!.Pedidos().Select(p=>$"{p.Chave[..8]} · {p.Estado} · servidor {p.PedidoServidorId} · {p.Erro}"));return Task.CompletedTask;});
  Botao("Pedidos do servidor (cache)",()=>{status.Text=loja!.Cache("pedidos")?.Json??"Atualize o cache.";return Task.CompletedTask;});
  Botao("Sair",()=>{SecureStorage.Default.Remove("token");SecureStorage.Default.Remove("cache");loja?.Dispose();loja=null;client?.Dispose();carrinho.Clear();Application.Current!.Windows[0].Page=new NavigationPage(new MainPage());return Task.CompletedTask;});
  Carregar();
 }
 List<JsonElement> Lista(string key){using var j=JsonDocument.Parse(loja!.Cache(key)?.Json??"[]");return j.RootElement.EnumerateArray().Select(e=>e.Clone()).ToList();}
 async Task Roteiro(){var stack=new VerticalStackLayout{Padding=20,Spacing=14};foreach(var c in Lista("clientes"))stack.Add(new Label{Text=$"{c.GetProperty("ordemVisita")} · {c.GetProperty("razaoSocial")}\n{c.GetProperty("endereco")} · {c.GetProperty("cidade")}"});await Navigation.PushAsync(new ContentPage{Title="Roteiro (cache)",Content=new ScrollView{Content=stack}});}
}
