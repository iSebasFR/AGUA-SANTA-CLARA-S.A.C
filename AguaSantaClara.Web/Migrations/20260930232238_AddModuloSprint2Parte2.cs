using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloSprint2Parte2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "categoria",
                table: "producto",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_retornable",
                table: "producto",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "stock_actual",
                table: "insumo",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "stock_minimo",
                table: "insumo",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "deuda_total",
                table: "cliente",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "saldo_bidones",
                table: "cliente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "deuda",
                columns: table => new
                {
                    id_deuda = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<long>(type: "bigint", nullable: false),
                    id_cliente = table.Column<long>(type: "bigint", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deuda", x => x.id_deuda);
                    table.CheckConstraint("chk_deuda_estado", "estado IN ('Pendiente', 'Pagada', 'Vencida')");
                    table.CheckConstraint("chk_deuda_monto_positivo", "monto > 0");
                    table.ForeignKey(
                        name: "FK_deuda_cliente_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "cliente",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deuda_pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incidencia",
                columns: table => new
                {
                    id_incidencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<long>(type: "bigint", nullable: false),
                    motivo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidencia", x => x.id_incidencia);
                    table.CheckConstraint("chk_incidencia_motivo", "motivo IN ('Cliente ausente', 'Dirección incorrecta', 'Producto dañado', 'Vehículo descompuesto', 'Otros')");
                    table.ForeignKey(
                        name: "FK_incidencia_pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metodo_pago",
                columns: table => new
                {
                    id_metodo_pago = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metodo_pago", x => x.id_metodo_pago);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_bidones",
                columns: table => new
                {
                    id_movimiento = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_cliente = table.Column<long>(type: "bigint", nullable: false),
                    bidones_entregados = table.Column<int>(type: "integer", nullable: false),
                    bidones_devueltos = table.Column<int>(type: "integer", nullable: false),
                    saldo = table.Column<int>(type: "integer", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_bidones", x => x.id_movimiento);
                    table.CheckConstraint("chk_movimiento_bidones_devueltos", "bidones_devueltos >= 0");
                    table.CheckConstraint("chk_movimiento_bidones_entregados", "bidones_entregados >= 0");
                    table.ForeignKey(
                        name: "FK_movimiento_bidones_cliente_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "cliente",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pago",
                columns: table => new
                {
                    id_pago = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<long>(type: "bigint", nullable: false),
                    id_cliente = table.Column<long>(type: "bigint", nullable: false),
                    id_metodo_pago = table.Column<long>(type: "bigint", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha_pago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pago", x => x.id_pago);
                    table.CheckConstraint("chk_pago_monto_positivo", "monto > 0");
                    table.ForeignKey(
                        name: "FK_pago_cliente_id_cliente",
                        column: x => x.id_cliente,
                        principalTable: "cliente",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pago_metodo_pago_id_metodo_pago",
                        column: x => x.id_metodo_pago,
                        principalTable: "metodo_pago",
                        principalColumn: "id_metodo_pago",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pago_pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_deuda_cliente",
                table: "deuda",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "idx_deuda_estado",
                table: "deuda",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "idx_deuda_vencimiento",
                table: "deuda",
                column: "fecha_vencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_deuda_id_pedido",
                table: "deuda",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "idx_incidencia_pedido",
                table: "incidencia",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "uq_metodo_pago_nombre",
                table: "metodo_pago",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_movimiento_bidones_cliente",
                table: "movimiento_bidones",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "idx_pago_cliente",
                table: "pago",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "idx_pago_pedido",
                table: "pago",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_pago_id_metodo_pago",
                table: "pago",
                column: "id_metodo_pago");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deuda");

            migrationBuilder.DropTable(
                name: "incidencia");

            migrationBuilder.DropTable(
                name: "movimiento_bidones");

            migrationBuilder.DropTable(
                name: "pago");

            migrationBuilder.DropTable(
                name: "metodo_pago");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "producto");

            migrationBuilder.DropColumn(
                name: "es_retornable",
                table: "producto");

            migrationBuilder.DropColumn(
                name: "stock_actual",
                table: "insumo");

            migrationBuilder.DropColumn(
                name: "stock_minimo",
                table: "insumo");

            migrationBuilder.DropColumn(
                name: "deuda_total",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "saldo_bidones",
                table: "cliente");
        }
    }
}
