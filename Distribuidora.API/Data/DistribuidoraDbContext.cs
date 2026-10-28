using Distribuidora.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Data;

public class DistribuidoraDbContext : DbContext
{
    public DistribuidoraDbContext(DbContextOptions<DistribuidoraDbContext> options)
        : base(options) { }

    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<GrupoProduto> GruposProduto => Set<GrupoProduto>();
    public DbSet<LoteEstoque> LotesEstoque => Set<LoteEstoque>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<AlocacaoLotePedido> AlocacoesLotePedido => Set<AlocacaoLotePedido>();
    public DbSet<LoteProcessamento> LotesProcessamento => Set<LoteProcessamento>();
    public DbSet<LoteProcessamentoPedido> LotesProcessamentoPedidos => Set<LoteProcessamentoPedido>();
    public DbSet<UsuarioSistema> Usuarios => Set<UsuarioSistema>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<CondicaoPagamento> CondicoesPagamento => Set<CondicaoPagamento>();
    public DbSet<ParcelaCondicao> ParcelasCondicao => Set<ParcelaCondicao>();
    public DbSet<ContaReceber> ContasReceber => Set<ContaReceber>();
    public DbSet<ParcelaReceber> ParcelasReceber => Set<ParcelaReceber>();
    public DbSet<Recebimento> Recebimentos => Set<Recebimento>();

    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RegistroAuditoria>(entity =>
        {
            entity.ToTable("RegistrosAuditoria");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.DataUtc).HasColumnType("datetime(6)");
            entity.Property(a => a.Perfil).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Metodo).HasMaxLength(10).IsRequired();
            entity.Property(a => a.Caminho).HasMaxLength(350).IsRequired();
            entity.Property(a => a.CorrelacaoId).HasMaxLength(100).IsRequired();
            entity.HasIndex(a => a.DataUtc);
            entity.HasIndex(a => new { a.UsuarioId, a.DataUtc });
        });

        modelBuilder.Entity<UsuarioSistema>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Nome).HasMaxLength(150).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(180).IsRequired();
            entity.Property(u => u.SenhaHash).HasMaxLength(512).IsRequired();
            entity.Property(u => u.Perfil).HasMaxLength(20).IsRequired();
            entity.Property(u => u.Ativo).HasDefaultValue(true);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.VendedorId).IsUnique();
            entity.HasOne(u => u.Vendedor)
                .WithMany()
                .HasForeignKey(u => u.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CondicaoPagamento>(entity =>
        {
            entity.ToTable("CondicoesPagamento");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nome).HasMaxLength(120).IsRequired();
            entity.Property(c => c.Ativa).HasDefaultValue(true);
            entity.HasIndex(c => c.Nome).IsUnique();
        });

        modelBuilder.Entity<ParcelaCondicao>(entity =>
        {
            entity.ToTable("ParcelasCondicao");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Percentual).HasPrecision(5, 2);
            entity.HasIndex(p => new { p.CondicaoPagamentoId, p.Numero }).IsUnique();
            entity.HasIndex(p => new { p.CondicaoPagamentoId, p.DiasAposVenda }).IsUnique();
            entity.HasOne(p => p.CondicaoPagamento)
                .WithMany(c => c.Parcelas)
                .HasForeignKey(p => p.CondicaoPagamentoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Venda>(entity =>
        {
            entity.ToTable("Vendas");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.DataEmissaoUtc).HasColumnType("datetime(6)");
            entity.Property(v => v.ValorTotal).HasPrecision(18, 2);
            entity.HasIndex(v => v.PedidoId).IsUnique();
            entity.HasOne(v => v.Pedido).WithMany()
                .HasForeignKey(v => v.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemVenda>(entity =>
        {
            entity.ToTable("ItensVenda");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
            entity.HasIndex(i => i.ItemPedidoId).IsUnique();
            entity.HasIndex(i => new { i.VendaId, i.ItemPedidoId }).IsUnique();
            entity.HasOne(i => i.Venda).WithMany(v => v.Itens)
                .HasForeignKey(i => i.VendaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.ItemPedido).WithMany()
                .HasForeignKey(i => i.ItemPedidoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContaReceber>(entity =>
        {
            entity.ToTable("ContasReceber");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.DataCriacaoUtc).HasColumnType("datetime(6)");
            entity.Property(c => c.ValorOriginal).HasPrecision(18, 2);
            entity.HasIndex(c => c.VendaId).IsUnique();
            entity.HasOne(c => c.Venda).WithMany()
                .HasForeignKey(c => c.VendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.CondicaoPagamento).WithMany()
                .HasForeignKey(c => c.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParcelaReceber>(entity =>
        {
            entity.ToTable("ParcelasReceber");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Vencimento).HasColumnType("date");
            entity.Property(p => p.ValorOriginal).HasPrecision(18, 2);
            entity.Property(p => p.ValorPago).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.HasIndex(p => new { p.ContaReceberId, p.Numero }).IsUnique();
            entity.HasIndex(p => p.Vencimento);
            entity.HasOne(p => p.ContaReceber).WithMany(c => c.Parcelas)
                .HasForeignKey(p => p.ContaReceberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Recebimento>(entity =>
        {
            entity.ToTable("Recebimentos");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ChaveOperacao).HasMaxLength(36).IsRequired();
            entity.Property(r => r.Valor).HasPrecision(18, 2);
            entity.Property(r => r.FormaPagamento).HasMaxLength(40).IsRequired();
            entity.Property(r => r.Observacao).HasMaxLength(200).IsRequired();
            entity.Property(r => r.DataUtc).HasColumnType("datetime(6)");
            entity.HasIndex(r => r.ChaveOperacao).IsUnique();
            entity.HasIndex(r => new { r.ParcelaReceberId, r.DataUtc });
            entity.HasOne(r => r.ParcelaReceber).WithMany(p => p.Recebimentos)
                .HasForeignKey(r => r.ParcelaReceberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Vendedor>(entity =>
        {
            entity.ToTable("Vendedores");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Nome).HasMaxLength(150).IsRequired();
            entity.Property(v => v.Email).HasMaxLength(180).IsRequired();
            entity.HasIndex(v => v.Email).IsUnique();
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.RazaoSocial).HasMaxLength(180).IsRequired();
            entity.Property(c => c.Cnpj).HasMaxLength(14).IsRequired();
            entity.HasIndex(c => c.Cnpj).IsUnique();
            entity.Property(c => c.Ativo).HasDefaultValue(true);
            entity.HasOne(c => c.Vendedor)
                .WithMany(v => v.Clientes)
                .HasForeignKey(c => c.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrupoProduto>(entity =>
        {
            entity.ToTable("GruposProduto");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Nome).HasMaxLength(120).IsRequired();
            entity.Property(g => g.Descricao).HasMaxLength(300);
            entity.Property(g => g.Ativo).HasDefaultValue(true);
            entity.HasIndex(g => g.Nome).IsUnique();
        });

        modelBuilder.Entity<Produto>(entity =>
        {
            entity.ToTable("Produtos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nome).HasMaxLength(150).IsRequired();
            entity.Property(p => p.UnidadeMedida).HasMaxLength(10).IsRequired();
            entity.Property(p => p.PrecoVenda).HasPrecision(18, 2);

            // Produto antigo pode permanecer sem grupo; a API exige grupo nos novos.
            entity.HasOne(p => p.GrupoProduto)
                .WithMany(g => g.Produtos)
                .HasForeignKey(p => p.GrupoProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoteEstoque>(entity =>
        {
            entity.ToTable("LotesEstoque");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.CodigoLote).HasMaxLength(50).IsRequired();
            entity.Property(l => l.DataValidade).HasColumnType("date");
            entity.Property(l => l.DataEntradaUtc).HasColumnType("datetime(6)");
            entity.HasIndex(l => new { l.ProdutoId, l.CodigoLote }).IsUnique();
            entity.HasIndex(l => new { l.ProdutoId, l.DataValidade });
            entity.HasOne(l => l.Produto)
                .WithMany(p => p.Lotes)
                .HasForeignKey(l => l.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.ToTable("MovimentacoesEstoque");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Tipo).HasConversion<int>();
            entity.Property(m => m.ChaveOperacao).HasMaxLength(36).IsRequired();
            entity.Property(m => m.Observacao).HasMaxLength(300).IsRequired();
            entity.Property(m => m.DataUtc).HasColumnType("datetime(6)");
            entity.HasIndex(m => m.ChaveOperacao).IsUnique();
            entity.HasIndex(m => new { m.LoteEstoqueId, m.DataUtc });
            entity.HasOne(m => m.LoteEstoque)
                .WithMany()
                .HasForeignKey(m => m.LoteEstoqueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlocacaoLotePedido>(entity =>
        {
            entity.ToTable("AlocacoesLotePedido");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.ItemPedidoId, a.LoteEstoqueId }).IsUnique();
            entity.HasIndex(a => a.MovimentacaoEstoqueId).IsUnique();
            entity.HasOne(a => a.ItemPedido)
                .WithMany()
                .HasForeignKey(a => a.ItemPedidoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.LoteEstoque)
                .WithMany()
                .HasForeignKey(a => a.LoteEstoqueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.MovimentacaoEstoque)
                .WithMany()
                .HasForeignKey(a => a.MovimentacaoEstoqueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoteProcessamento>(entity =>
        {
            entity.ToTable("LotesProcessamento");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.DataCorte).HasColumnType("date");
            entity.Property(l => l.InicioUtc).HasColumnType("datetime(6)");
            entity.Property(l => l.FimUtc).HasColumnType("datetime(6)");
            entity.Property(l => l.DataFechamentoUtc).HasColumnType("datetime(6)");
            entity.Property(l => l.DataProcessamentoUtc).HasColumnType("datetime(6)");
            entity.HasIndex(l => new { l.DataCorte, l.HoraCorte }).IsUnique();
        });

        modelBuilder.Entity<LoteProcessamentoPedido>(entity =>
        {
            entity.ToTable("LotesProcessamentoPedidos");
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.PedidoId).IsUnique();
            entity.HasIndex(v => new { v.LoteProcessamentoId, v.PedidoId }).IsUnique();
            entity.HasOne(v => v.LoteProcessamento)
                .WithMany(l => l.VinculosPedidos)
                .HasForeignKey(v => v.LoteProcessamentoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.Pedido)
                .WithMany()
                .HasForeignKey(v => v.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("Pedidos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Status).HasConversion<int>();
            entity.Property(p => p.ChaveRequisicao).HasMaxLength(36);
            entity.HasIndex(p => p.ChaveRequisicao).IsUnique();
            entity.HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.Vendedor)
                .WithMany()
                .HasForeignKey(p => p.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemPedido>(entity =>
        {
            entity.ToTable("ItensPedido");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
            entity.HasOne(i => i.Pedido)
                .WithMany(p => p.Itens)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Produto)
                .WithMany()
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(i => new { i.PedidoId, i.ProdutoId }).IsUnique();
        });
    }
}
