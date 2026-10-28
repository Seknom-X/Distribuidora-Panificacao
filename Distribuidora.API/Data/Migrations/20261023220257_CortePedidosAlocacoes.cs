using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Distribuidora.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CortePedidosAlocacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlocacoesLotePedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ItemPedidoId = table.Column<int>(type: "int", nullable: false),
                    LoteEstoqueId = table.Column<int>(type: "int", nullable: false),
                    MovimentacaoEstoqueId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlocacoesLotePedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlocacoesLotePedido_ItensPedido_ItemPedidoId",
                        column: x => x.ItemPedidoId,
                        principalTable: "ItensPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlocacoesLotePedido_LotesEstoque_LoteEstoqueId",
                        column: x => x.LoteEstoqueId,
                        principalTable: "LotesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlocacoesLotePedido_MovimentacoesEstoque_MovimentacaoEstoque~",
                        column: x => x.MovimentacaoEstoqueId,
                        principalTable: "MovimentacoesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesLotePedido_ItemPedidoId_LoteEstoqueId",
                table: "AlocacoesLotePedido",
                columns: new[] { "ItemPedidoId", "LoteEstoqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesLotePedido_LoteEstoqueId",
                table: "AlocacoesLotePedido",
                column: "LoteEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesLotePedido_MovimentacaoEstoqueId",
                table: "AlocacoesLotePedido",
                column: "MovimentacaoEstoqueId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlocacoesLotePedido");
        }
    }
}
