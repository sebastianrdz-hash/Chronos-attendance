using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Chronos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class InicialChronos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_para_mostrar = table.Column<string>(type: "text", nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_acceso_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sedes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    zona_horaria = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    geocerca_latitud = table.Column<double>(type: "double precision", nullable: true),
                    geocerca_longitud = table.Column<double>(type: "double precision", nullable: true),
                    geocerca_radio_metros = table.Column<int>(type: "integer", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sedes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turnos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    hora_entrada = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_salida = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    tolerancia_minutos = table.Column<int>(type: "integer", nullable: false),
                    minutos_descanso = table.Column<int>(type: "integer", nullable: false),
                    dias_laborales = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turnos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "departamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_departamentos_sedes_sede_id",
                        column: x => x.sede_id,
                        principalTable: "sedes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "empleados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_empleado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombres = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    apellido_paterno = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    apellido_materno = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    correo_corporativo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    puesto = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_baja = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    departamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empleados", x => x.id);
                    table.ForeignKey(
                        name: "fk_empleados_departamentos_departamento_id",
                        column: x => x.departamento_id,
                        principalTable: "departamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_empleados_sedes_sede_id",
                        column: x => x.sede_id,
                        principalTable: "sedes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_empleados_turnos_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "checadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    momento_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dia_laboral = table.Column<DateOnly>(type: "date", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    puntaje_confianza = table.Column<int>(type: "integer", nullable: false),
                    nivel_confianza = table.Column<int>(type: "integer", nullable: false),
                    huella_dispositivo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    direccion_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ajustada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_ajuste = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_checadas_empleados_empleado_id",
                        column: x => x.empleado_id,
                        principalTable: "empleados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_checadas_sedes_sede_id",
                        column: x => x.sede_id,
                        principalTable: "sedes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "credenciales_web_authn",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    clave_publica = table.Column<byte[]>(type: "bytea", nullable: false),
                    id_usuario = table.Column<byte[]>(type: "bytea", nullable: false),
                    contador_firmas = table.Column<long>(type: "bigint", nullable: false),
                    aa_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_amigable = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tipo_dispositivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_uso_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credenciales_web_authn", x => x.id);
                    table.ForeignKey(
                        name: "fk_credenciales_web_authn_empleados_empleado_id",
                        column: x => x.empleado_id,
                        principalTable: "empleados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "senales_presencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    checada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    peso_aplicado = table.Column<int>(type: "integer", nullable: false),
                    capturada_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    detalle_json = table.Column<string>(type: "jsonb", nullable: true),
                    creado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_senales_presencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_senales_presencia_checadas_checada_id",
                        column: x => x.checada_id,
                        principalTable: "checadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_checadas_empleado_id_dia_laboral",
                table: "checadas",
                columns: new[] { "empleado_id", "dia_laboral" });

            migrationBuilder.CreateIndex(
                name: "ix_checadas_estado",
                table: "checadas",
                column: "estado",
                filter: "estado = 2");

            migrationBuilder.CreateIndex(
                name: "ix_checadas_momento_utc",
                table: "checadas",
                column: "momento_utc");

            migrationBuilder.CreateIndex(
                name: "ix_checadas_sede_id",
                table: "checadas",
                column: "sede_id");

            migrationBuilder.CreateIndex(
                name: "ix_credenciales_web_authn_credential_id",
                table: "credenciales_web_authn",
                column: "credential_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credenciales_web_authn_empleado_id",
                table: "credenciales_web_authn",
                column: "empleado_id");

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_sede_id_codigo",
                table: "departamentos",
                columns: new[] { "sede_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_correo_corporativo",
                table: "empleados",
                column: "correo_corporativo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_departamento_id",
                table: "empleados",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_empleados_numero_empleado",
                table: "empleados",
                column: "numero_empleado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_sede_id",
                table: "empleados",
                column: "sede_id");

            migrationBuilder.CreateIndex(
                name: "ix_empleados_turno_id",
                table: "empleados",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "ix_empleados_usuario_id",
                table: "empleados",
                column: "usuario_id",
                unique: true,
                filter: "usuario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sedes_codigo",
                table: "sedes",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_senales_presencia_checada_id_tipo",
                table: "senales_presencia",
                columns: new[] { "checada_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "ix_senales_presencia_tipo",
                table: "senales_presencia",
                column: "tipo");

            migrationBuilder.CreateIndex(
                name: "ix_turnos_nombre",
                table: "turnos",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "credenciales_web_authn");

            migrationBuilder.DropTable(
                name: "senales_presencia");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "checadas");

            migrationBuilder.DropTable(
                name: "empleados");

            migrationBuilder.DropTable(
                name: "departamentos");

            migrationBuilder.DropTable(
                name: "turnos");

            migrationBuilder.DropTable(
                name: "sedes");
        }
    }
}
