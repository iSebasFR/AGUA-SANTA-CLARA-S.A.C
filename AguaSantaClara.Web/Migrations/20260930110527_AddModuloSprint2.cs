using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloSprint2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "url_ubicacion",
                table: "direccion_cliente",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dni",
                table: "cliente",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "local",
                columns: table => new
                {
                    id_local = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local", x => x.id_local);
                });

            migrationBuilder.CreateTable(
                name: "repartidor",
                columns: table => new
                {
                    id_repartidor = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    celular = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repartidor", x => x.id_repartidor);
                    table.CheckConstraint("chk_repartidor_celular", "celular ~ '^519[0-9]{8}$'");
                });

            migrationBuilder.CreateTable(
                name: "producto_local",
                columns: table => new
                {
                    id_producto_local = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_local = table.Column<long>(type: "bigint", nullable: false),
                    id_producto = table.Column<long>(type: "bigint", nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_local", x => x.id_producto_local);
                    table.CheckConstraint("chk_producto_local_stock_no_negativo", "stock >= 0");
                    table.ForeignKey(
                        name: "FK_producto_local_local_id_local",
                        column: x => x.id_local,
                        principalTable: "local",
                        principalColumn: "id_local",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_producto_local_producto_id_producto",
                        column: x => x.id_producto,
                        principalTable: "producto",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido",
                columns: table => new
                {
                    id_pedido = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_local = table.Column<long>(type: "bigint", nullable: false),
                    id_repartidor = table.Column<long>(type: "bigint", nullable: true),
                    fecha_entrega = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido", x => x.id_pedido);
                    table.CheckConstraint("chk_pedido_estado", "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia', 'Pagado', 'Pago Parcial')");
                    table.CheckConstraint("chk_pedido_total_no_negativo", "total >= 0");
                    table.ForeignKey(
                        name: "FK_pedido_local_id_local",
                        column: x => x.id_local,
                        principalTable: "local",
                        principalColumn: "id_local",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedido_repartidor_id_repartidor",
                        column: x => x.id_repartidor,
                        principalTable: "repartidor",
                        principalColumn: "id_repartidor",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_cliente",
                columns: table => new
                {
                    id_pedido_cliente = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<long>(type: "bigint", nullable: false),
                    id_cliente = table.Column<long>(type: "bigint", nullable: false),
                    id_direccion = table.Column<long>(type: "bigint", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_cliente", x => x.id_pedido_cliente);
                    table.CheckConstraint("chk_pedido_cliente_subtotal_no_negativo", "subtotal >= 0");
                    table.ForeignKey(
                        name: "FK_pedido_cliente_cliente_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "cliente",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedido_cliente_direccion_cliente_id_direccion",
                        column: x => x.id_direccion,
                        principalTable: "direccion_cliente",
                        principalColumn: "id_direccion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedido_cliente_pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detalle_pedido",
                columns: table => new
                {
                    id_detalle_pedido = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido_cliente = table.Column<long>(type: "bigint", nullable: false),
                    id_producto = table.Column<long>(type: "bigint", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_pedido", x => x.id_detalle_pedido);
                    table.CheckConstraint("chk_detalle_pedido_cantidad_positiva", "cantidad > 0");
                    table.CheckConstraint("chk_detalle_pedido_precio_positivo", "precio_unitario > 0");
                    table.ForeignKey(
                        name: "FK_detalle_pedido_pedido_cliente_id_pedido_cliente",
                        column: x => x.id_pedido_cliente,
                        principalTable: "pedido_cliente",
                        principalColumn: "id_pedido_cliente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_pedido_producto_id_producto",
                        column: x => x.id_producto,
                        principalTable: "producto",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_cliente_dni",
                table: "cliente",
                column: "dni",
                unique: true,
                filter: "dni IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_cliente_dni",
                table: "cliente",
                sql: "dni IS NULL OR dni ~ '^[0-9]{8}$'");

            migrationBuilder.CreateIndex(
                name: "idx_detalle_pedido_pedido_cliente",
                table: "detalle_pedido",
                column: "id_pedido_cliente");

            migrationBuilder.CreateIndex(
                name: "idx_detalle_pedido_producto",
                table: "detalle_pedido",
                column: "id_producto");

            migrationBuilder.CreateIndex(
                name: "uq_local_nombre",
                table: "local",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pedido_estado",
                table: "pedido",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "idx_pedido_fecha_entrega",
                table: "pedido",
                column: "fecha_entrega");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_id_local",
                table: "pedido",
                column: "id_local");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_id_repartidor",
                table: "pedido",
                column: "id_repartidor");

            migrationBuilder.CreateIndex(
                name: "idx_pedido_cliente_cliente",
                table: "pedido_cliente",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_cliente_id_direccion",
                table: "pedido_cliente",
                column: "id_direccion");

            migrationBuilder.CreateIndex(
                name: "uq_pedido_cliente_direccion",
                table: "pedido_cliente",
                columns: new[] { "id_pedido", "id_cliente", "id_direccion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_producto_local_id_producto",
                table: "producto_local",
                column: "id_producto");

            migrationBuilder.CreateIndex(
                name: "uq_producto_local",
                table: "producto_local",
                columns: new[] { "id_local", "id_producto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_repartidor_celular",
                table: "repartidor",
                column: "celular",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalle_pedido");

            migrationBuilder.DropTable(
                name: "producto_local");

            migrationBuilder.DropTable(
                name: "pedido_cliente");

            migrationBuilder.DropTable(
                name: "pedido");

            migrationBuilder.DropTable(
                name: "local");

            migrationBuilder.DropTable(
                name: "repartidor");

            migrationBuilder.DropIndex(
                name: "uq_cliente_dni",
                table: "cliente");

            migrationBuilder.DropCheckConstraint(
                name: "chk_cliente_dni",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "url_ubicacion",
                table: "direccion_cliente");

            migrationBuilder.DropColumn(
                name: "dni",
                table: "cliente");
        }
    }
}
