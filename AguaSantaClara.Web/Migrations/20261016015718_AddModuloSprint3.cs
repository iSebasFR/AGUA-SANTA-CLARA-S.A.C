using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloSprint3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_autoventa",
                table: "pedido",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "autorizacion_plazo",
                columns: table => new
                {
                    id_autorizacion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_deuda = table.Column<long>(type: "bigint", nullable: false),
                    id_usuario_autoriza = table.Column<long>(type: "bigint", nullable: false),
                    monto_anterior = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    monto_nuevo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha_vencimiento_anterior = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_vencimiento_nueva = table.Column<DateOnly>(type: "date", nullable: false),
                    observacion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_autorizacion_plazo", x => x.id_autorizacion);
                    table.CheckConstraint("chk_autorizacion_monto_anterior_positivo", "monto_anterior > 0");
                    table.CheckConstraint("chk_autorizacion_monto_nuevo_positivo", "monto_nuevo > 0");
                    table.ForeignKey(
                        name: "FK_autorizacion_plazo_deuda_id_deuda",
                        column: x => x.id_deuda,
                        principalTable: "deuda",
                        principalColumn: "id_deuda",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_autorizacion_plazo_usuario_id_usuario_autoriza",
                        column: x => x.id_usuario_autoriza,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cierre_caja",
                columns: table => new
                {
                    id_cierre = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_vendedora = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    efectivo_recibido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    yape_recibido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gastos_del_dia = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    efectivo_esperado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    yape_esperado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cierre_caja", x => x.id_cierre);
                    table.CheckConstraint("chk_cierre_efectivo_no_negativo", "efectivo_recibido >= 0");
                    table.CheckConstraint("chk_cierre_estado", "estado IN ('Cuadrado', 'Con diferencia')");
                    table.CheckConstraint("chk_cierre_gastos_no_negativo", "gastos_del_dia >= 0");
                    table.CheckConstraint("chk_cierre_yape_no_negativo", "yape_recibido >= 0");
                    table.ForeignKey(
                        name: "FK_cierre_caja_usuario_id_vendedora",
                        column: x => x.id_vendedora,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gasto_ruta",
                columns: table => new
                {
                    id_gasto = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_repartidor = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    observacion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estado_registro = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto_ruta", x => x.id_gasto);
                    table.CheckConstraint("chk_gasto_ruta_monto_positivo", "monto > 0");
                    table.CheckConstraint("chk_gasto_ruta_tipo", "tipo IN ('Gasolina', 'Peajes', 'Cochera', 'Otros')");
                    table.ForeignKey(
                        name: "FK_gasto_ruta_repartidor_id_repartidor",
                        column: x => x.id_repartidor,
                        principalTable: "repartidor",
                        principalColumn: "id_repartidor",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_pedido_es_autoventa",
                table: "pedido",
                column: "es_autoventa");

            migrationBuilder.CreateIndex(
                name: "idx_autorizacion_deuda",
                table: "autorizacion_plazo",
                column: "id_deuda");

            migrationBuilder.CreateIndex(
                name: "idx_autorizacion_usuario",
                table: "autorizacion_plazo",
                column: "id_usuario_autoriza");

            migrationBuilder.CreateIndex(
                name: "idx_cierre_caja_fecha",
                table: "cierre_caja",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "idx_cierre_caja_vendedora",
                table: "cierre_caja",
                column: "id_vendedora");

            migrationBuilder.CreateIndex(
                name: "uq_cierre_caja_vendedora_fecha",
                table: "cierre_caja",
                columns: new[] { "id_vendedora", "fecha" },
                unique: true,
                filter: "estado_registro = true");

            migrationBuilder.CreateIndex(
                name: "idx_gasto_ruta_fecha",
                table: "gasto_ruta",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "idx_gasto_ruta_repartidor",
                table: "gasto_ruta",
                column: "id_repartidor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "autorizacion_plazo");

            migrationBuilder.DropTable(
                name: "cierre_caja");

            migrationBuilder.DropTable(
                name: "gasto_ruta");

            migrationBuilder.DropIndex(
                name: "idx_pedido_es_autoventa",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "es_autoventa",
                table: "pedido");
        }
    }
}
