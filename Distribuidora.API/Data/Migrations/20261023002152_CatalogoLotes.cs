using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Distribuidora.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoLotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrupoProdutoId",
                table: "Produtos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GruposProduto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descricao = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposProduto", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LotesEstoque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    CodigoLote = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataValidade = table.Column<DateOnly>(type: "date", nullable: false),
                    DataEntradaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    QuantidadeDisponivel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesEstoque_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_GrupoProdutoId",
                table: "Produtos",
                column: "GrupoProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposProduto_Nome",
                table: "GruposProduto",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesEstoque_ProdutoId_CodigoLote",
                table: "LotesEstoque",
                columns: new[] { "ProdutoId", "CodigoLote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesEstoque_ProdutoId_DataValidade",
                table: "LotesEstoque",
                columns: new[] { "ProdutoId", "DataValidade" });

            migrationBuilder.AddForeignKey(
                name: "FK_Produtos_GruposProduto_GrupoProdutoId",
                table: "Produtos",
                column: "GrupoProdutoId",
                principalTable: "GruposProduto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Produtos_GruposProduto_GrupoProdutoId",
                table: "Produtos");

            migrationBuilder.DropTable(
                name: "GruposProduto");

            migrationBuilder.DropTable(
                name: "LotesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_GrupoProdutoId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "GrupoProdutoId",
                table: "Produtos");
        }
    }
}
