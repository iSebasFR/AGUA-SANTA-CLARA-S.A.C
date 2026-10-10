using AguaSantaClara.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261009230000_AddMontoPagadoAlRegistrar")]
public partial class AddMontoPagadoAlRegistrar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "monto_pagado_al_registrar",
            table: "deuda",
            type: "numeric(12,2)",
            precision: 12,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddCheckConstraint(
            name: "chk_deuda_monto_pagado_no_negativo",
            table: "deuda",
            sql: "monto_pagado_al_registrar >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "chk_deuda_monto_pagado_no_negativo",
            table: "deuda");

        migrationBuilder.DropColumn(
            name: "monto_pagado_al_registrar",
            table: "deuda");
    }
}
