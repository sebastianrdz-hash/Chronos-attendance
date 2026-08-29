using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DesafiosWebAuthn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "desafios_webauthn",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposito = table.Column<int>(type: "integer", nullable: false),
                    opciones_json = table.Column<string>(type: "jsonb", nullable: false),
                    nombre_dispositivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desafios_webauthn", x => x.id);
                    table.ForeignKey(
                        name: "fk_desafios_webauthn_empleados_empleado_id",
                        column: x => x.empleado_id,
                        principalTable: "empleados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_desafios_webauthn_empleado_id_proposito",
                table: "desafios_webauthn",
                columns: new[] { "empleado_id", "proposito" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_desafios_webauthn_expira_utc",
                table: "desafios_webauthn",
                column: "expira_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "desafios_webauthn");
        }
    }
}
