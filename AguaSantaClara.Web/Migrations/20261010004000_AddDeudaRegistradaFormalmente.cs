using AguaSantaClara.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261010004000_AddDeudaRegistradaFormalmente")]
public partial class AddDeudaRegistradaFormalmente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "registrada_formalmente",
            table: "deuda",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "registrada_formalmente",
            table: "deuda");
    }
}
