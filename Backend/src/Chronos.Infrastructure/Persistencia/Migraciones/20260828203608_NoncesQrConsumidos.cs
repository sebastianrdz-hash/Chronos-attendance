using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class NoncesQrConsumidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nonces_qr_consumidos",
                columns: table => new
                {
                    nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumido_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nonces_qr_consumidos", x => x.nonce);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nonces_qr_consumidos_expira_utc",
                table: "nonces_qr_consumidos",
                column: "expira_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nonces_qr_consumidos");
        }
    }
}
