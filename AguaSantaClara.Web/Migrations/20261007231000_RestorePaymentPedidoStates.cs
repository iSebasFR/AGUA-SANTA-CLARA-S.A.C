using AguaSantaClara.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261007231000_RestorePaymentPedidoStates")]
public partial class RestorePaymentPedidoStates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "chk_pedido_estado",
            table: "pedido");

        migrationBuilder.AddCheckConstraint(
            name: "chk_pedido_estado",
            table: "pedido",
            sql: "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia', 'Pagado', 'Pago Parcial')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "chk_pedido_estado",
            table: "pedido");

        migrationBuilder.AddCheckConstraint(
            name: "chk_pedido_estado",
            table: "pedido",
            sql: "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia')");
    }
}
