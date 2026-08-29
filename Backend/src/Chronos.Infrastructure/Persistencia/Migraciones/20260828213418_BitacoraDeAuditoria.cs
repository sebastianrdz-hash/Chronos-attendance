using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class BitacoraDeAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitacora",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_correo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    accion = table.Column<int>(type: "integer", nullable: false),
                    entidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    datos_json = table.Column<string>(type: "jsonb", nullable: true),
                    direccion_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitacora", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_entidad_entidad_id",
                table: "bitacora",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_ocurrido_utc",
                table: "bitacora",
                column: "ocurrido_utc",
                descending: new bool[0]);

            // La inmutabilidad se impone en la base y no solo en el código. Que la
            // aplicación no intente modificar asientos es fácil de garantizar hoy y fácil
            // de romper con el tiempo; un disparador protege también contra el psql de
            // alguien con prisa. Sin esto, "solo inserción" sería una nota en el README.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION bitacora_es_inmutable()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'La bitácora es de solo inserción: % no está permitido.', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER bitacora_sin_modificaciones
                BEFORE UPDATE OR DELETE ON bitacora
                FOR EACH ROW EXECUTE FUNCTION bitacora_es_inmutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El disparador se quita antes que la tabla: DROP TABLE se lleva el disparador
            // por delante, pero la función es independiente y quedaría huérfana.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS bitacora_sin_modificaciones ON bitacora;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS bitacora_es_inmutable();");

            migrationBuilder.DropTable(
                name: "bitacora");
        }
    }
}
