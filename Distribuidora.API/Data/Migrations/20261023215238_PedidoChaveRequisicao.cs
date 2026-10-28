using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Distribuidora.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PedidoChaveRequisicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveRequisicao",
                table: "Pedidos",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ChaveRequisicao",
                table: "Pedidos",
                column: "ChaveRequisicao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_ChaveRequisicao",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ChaveRequisicao",
                table: "Pedidos");
        }
    }
}
