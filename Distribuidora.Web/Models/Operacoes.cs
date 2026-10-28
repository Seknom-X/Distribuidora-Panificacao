namespace Distribuidora.Web.Models;
public record Campo(string Nome, string Rotulo, string Tipo = "text");
public record Operacao(string Nome, string Rota, string Metodo, Campo[] Campos);
public static class Operacoes {
 public static readonly Dictionary<string, Operacao> Todas = new() {
 ["vendedores"] = new("Vendedores", "api/vendedores", "CRUD", [new("nome","Nome"),new("email","E-mail","email"),new("telefone","Telefone")]),
 ["clientes"] = new("Clientes", "api/clientes", "CRUD", [new("razaoSocial","Razão social"),new("cnpj","CNPJ (14 dígitos)"),new("telefone","Telefone"),new("endereco","Endereço"),new("cidade","Cidade"),new("estado","UF"),new("ordemVisita","Ordem da visita","number"),new("vendedorId","Vendedor","number")]),
 ["produtos"] = new("Produtos", "api/produtos", "CRUD", [new("nome","Nome"),new("unidadeMedida","Unidade"),new("precoVenda","Preço de venda","decimal"),new("grupoProdutoId","Grupo","number")]),
 ["grupos"] = new("Grupos de produtos", "api/grupos-produto", "CRUD", [new("nome","Nome"),new("descricao","Descrição")]),
 ["pedidos"] = new("Pedidos", "api/pedidos", "POST", [new("chaveRequisicao","Identificador","uuid"),new("clienteId","Cliente","number"),new("vendedorId","Vendedor","number"),new("itens","Produtos e quantidades","itens")]),
 ["vendas"] = new("Vendas", "api/vendas", "GET", []),
 ["janelas"] = new("Janelas de corte", "api/janelas-corte", "GET", []),
 ["lotes-processamento"] = new("Lotes de processamento", "api/lotes-processamento", "GET", []),
 ["auditoria"] = new("Auditoria", "api/admin/registros-auditoria", "GET", []),
 ["contas-pagina"] = new("Pesquisar contas por página", "api/admin/painel/contas?pagina={pagina}&tamanho={tamanho}", "GET", [new("pagina","Página","number"),new("tamanho","Registros por página (até 100)","number")]),
 ["contas"] = new("Contas a receber", "api/admin/painel/contas", "GET", []),
 ["financeiro"] = new("Conferência financeira", "api/admin/auditoria/financeiro", "GET", []),
 ["lotes"] = new("Entrada de lote", "api/produtos/{produtoId}/lotes", "POST", [new("produtoId","Produto","number"),new("codigoLote","Código do lote"),new("dataValidade","Validade","date"),new("quantidadeDisponivel","Quantidade","number")]),
 ["estoque"] = new("Resumo de estoque", "api/produtos/{produtoId}/estoque", "GET", [new("produtoId","Produto","number")]),
 ["movimentar"] = new("Movimentar estoque", "api/produtos/{produtoId}/lotes/{loteId}/movimentacoes", "POST", [new("produtoId","Produto","number"),new("loteId","Lote","number"),new("tipo","Tipo: 1 entrada, 2 saída","number"),new("quantidade","Quantidade","number"),new("chaveOperacao","Identificador","uuid"),new("observacao","Observação")]),
 ["fechar"] = new("Fechar janela", "api/lotes-processamento/fechar", "POST", [new("dataCorte","Data do corte","date"),new("horaCorte","Hora do corte","number")]),
 ["processar"] = new("Processar lote FEFO", "api/lotes-processamento/{id}/processar", "POST", [new("id","Lote de processamento","number")]),
 ["faturar"] = new("Gerar venda do pedido", "api/vendas/do-pedido/{id}", "POST", [new("id","Pedido atendido","number")]),
 ["condicoes"] = new("Condições de pagamento", "api/condicoes-pagamento", "POST", [new("nome","Nome"),new("parcelas","Parcelas: dias e percentual","parcelas")]),
 ["gerar-conta"] = new("Gerar conta a receber", "api/contas-receber/da-venda/{id}", "POST", [new("id","Venda","number"),new("condicaoPagamentoId","Condição de pagamento","number")]),
 ["conta"] = new("Consultar conta e parcelas", "api/contas-receber/{id}", "GET", [new("id","Conta","number")]),
 ["receber"] = new("Registrar recebimento", "api/contas-receber/parcelas/{id}/recebimentos", "POST", [new("id","Parcela","number"),new("chaveOperacao","Identificador","uuid"),new("valor","Valor recebido","decimal"),new("formaPagamento","Forma de pagamento"),new("observacao","Observação")]),
 ["usuario"] = new("Criar acesso do vendedor", "api/auth/usuarios/vendedor", "POST", [new("vendedorId","Vendedor","number"),new("nome","Nome"),new("email","E-mail","email"),new("senha","Senha (mínimo 12 caracteres)","password")]),
 ["reordenar"] = new("Reordenar roteiro", "api/vendedores/{id}/roteiro", "PUT", [new("id","Vendedor","number"),new("clienteIds","Todos os clientes ativos, em ordem (códigos separados por vírgula)","ids")]),
 ["roteiro"] = new("Consultar roteiro", "api/vendedores/{id}/roteiro", "GET", [new("id","Vendedor","number")])
 };
}
public record TelaPainel(string Chave, Operacao Operacao, System.Text.Json.JsonElement? Dados, Dictionary<string,string> Valores);



