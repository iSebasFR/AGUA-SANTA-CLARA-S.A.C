using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AguaSantaClara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAutorIncidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "id_usuario_reporta",
                table: "incidencia",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_incidencia_id_usuario_reporta",
                table: "incidencia",
                column: "id_usuario_reporta");

            migrationBuilder.AddForeignKey(
                name: "FK_incidencia_usuario_id_usuario_reporta",
                table: "incidencia",
                column: "id_usuario_reporta",
                principalTable: "usuario",
                principalColumn: "id_usuario",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incidencia_usuario_id_usuario_reporta",
                table: "incidencia");

            migrationBuilder.DropIndex(
                name: "IX_incidencia_id_usuario_reporta",
                table: "incidencia");

            migrationBuilder.DropColumn(
                name: "id_usuario_reporta",
                table: "incidencia");
        }
    }
}
