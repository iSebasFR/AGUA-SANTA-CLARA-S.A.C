using System;
using AguaSantaClara.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261003120000_QuitarFechaEntregaPedido")]
    public partial class QuitarFechaEntregaPedido : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_pedido_fecha_entrega",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "fecha_entrega",
                table: "pedido");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_entrega",
                table: "pedido",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "idx_pedido_fecha_entrega",
                table: "pedido",
                column: "fecha_entrega");
        }
    }
}
