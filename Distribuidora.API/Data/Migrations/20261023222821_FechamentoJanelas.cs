using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Distribuidora.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class FechamentoJanelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesProcessamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DataCorte = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraCorte = table.Column<int>(type: "int", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FimUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataFechamentoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesProcessamento", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LotesProcessamentoPedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LoteProcessamentoId = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesProcessamentoPedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesProcessamentoPedidos_LotesProcessamento_LoteProcessamen~",
                        column: x => x.LoteProcessamentoId,
                        principalTable: "LotesProcessamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesProcessamentoPedidos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LotesProcessamento_DataCorte_HoraCorte",
                table: "LotesProcessamento",
                columns: new[] { "DataCorte", "HoraCorte" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesProcessamentoPedidos_LoteProcessamentoId_PedidoId",
                table: "LotesProcessamentoPedidos",
                columns: new[] { "LoteProcessamentoId", "PedidoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesProcessamentoPedidos_PedidoId",
                table: "LotesProcessamentoPedidos",
                column: "PedidoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotesProcessamentoPedidos");

            migrationBuilder.DropTable(
                name: "LotesProcessamento");
        }
    }
}
