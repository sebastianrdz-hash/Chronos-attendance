using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ContrasenaTemporal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "debe_cambiar_contrasena",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "debe_cambiar_contrasena",
                table: "AspNetUsers");
        }
    }
}
