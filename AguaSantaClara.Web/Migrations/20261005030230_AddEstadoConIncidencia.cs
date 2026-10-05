using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoConIncidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido");

            migrationBuilder.AddCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido",
                sql: "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido");

            migrationBuilder.AddCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido",
                sql: "estado IN ('Pendiente', 'Enviado', 'Entregado')");
        }
    }
}
