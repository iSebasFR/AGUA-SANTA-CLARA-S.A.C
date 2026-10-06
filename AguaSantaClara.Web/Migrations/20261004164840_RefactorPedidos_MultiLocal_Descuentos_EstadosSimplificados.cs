using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPedidos_MultiLocal_Descuentos_EstadosSimplificados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pedido_local_id_local",
                table: "pedido");

            migrationBuilder.DropIndex(
                name: "IX_pedido_id_local",
                table: "pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_detalle_pedido_cantidad_positiva",
                table: "detalle_pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_detalle_pedido_precio_positivo",
                table: "detalle_pedido");

            migrationBuilder.DropColumn(
                name: "id_local",
                table: "pedido");

            migrationBuilder.RenameIndex(
                name: "idx_detalle_pedido_producto",
                table: "detalle_pedido",
                newName: "idx_detalle_producto");

            migrationBuilder.RenameIndex(
                name: "idx_detalle_pedido_pedido_cliente",
                table: "detalle_pedido",
                newName: "idx_detalle_pedido_cliente");

            migrationBuilder.AddColumn<decimal>(
                name: "descuento_monto",
                table: "detalle_pedido",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "id_local",
                table: "detalle_pedido",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido",
                sql: "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia', 'Pagado', 'Pago Parcial')");

            migrationBuilder.CreateIndex(
                name: "idx_detalle_local",
                table: "detalle_pedido",
                column: "id_local");

            migrationBuilder.AddCheckConstraint(
                name: "chk_detalle_cantidad_positiva",
                table: "detalle_pedido",
                sql: "cantidad > 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_detalle_descuento_no_negativo",
                table: "detalle_pedido",
                sql: "descuento_monto >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_detalle_subtotal_no_negativo",
                table: "detalle_pedido",
                sql: "subtotal >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_detalle_pedido_local_id_local",
                table: "detalle_pedido",
                column: "id_local",
                principalTable: "local",
                principalColumn: "id_local",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_detalle_pedido_local_id_local",
                table: "detalle_pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido");

            migrationBuilder.DropIndex(
                name: "idx_detalle_local",
                table: "detalle_pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_detalle_cantidad_positiva",
                table: "detalle_pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_detalle_descuento_no_negativo",
                table: "detalle_pedido");

            migrationBuilder.DropCheckConstraint(
                name: "chk_detalle_subtotal_no_negativo",
                table: "detalle_pedido");

            migrationBuilder.DropColumn(
                name: "descuento_monto",
                table: "detalle_pedido");

            migrationBuilder.DropColumn(
                name: "id_local",
                table: "detalle_pedido");

            migrationBuilder.RenameIndex(
                name: "idx_detalle_producto",
                table: "detalle_pedido",
                newName: "idx_detalle_pedido_producto");

            migrationBuilder.RenameIndex(
                name: "idx_detalle_pedido_cliente",
                table: "detalle_pedido",
                newName: "idx_detalle_pedido_pedido_cliente");

            migrationBuilder.AddColumn<long>(
                name: "id_local",
                table: "pedido",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedido_id_local",
                table: "pedido",
                column: "id_local");

            migrationBuilder.AddCheckConstraint(
                name: "chk_pedido_estado",
                table: "pedido",
                sql: "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia', 'Pagado', 'Pago Parcial')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_detalle_pedido_cantidad_positiva",
                table: "detalle_pedido",
                sql: "cantidad > 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_detalle_pedido_precio_positivo",
                table: "detalle_pedido",
                sql: "precio_unitario > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_pedido_local_id_local",
                table: "pedido",
                column: "id_local",
                principalTable: "local",
                principalColumn: "id_local",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
