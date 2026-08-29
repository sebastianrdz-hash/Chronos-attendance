-- Elimina los empleados que dejaron los scripts de verificación de la fase 2.
--
-- El sistema solo hace baja lógica por diseño: borrar el expediente de alguien se
-- llevaría por delante su historial de checadas, que es justo lo que un control de
-- asistencia debe conservar. Estos registros son la excepción porque nunca fueron
-- personas: los generó verificar.ps1 al probar el alta y la baja de empleados.
--
-- Se listan por correo, uno por uno, en vez de filtrar por un patrón como 'prueba.%'.
-- Un LIKE aquí es una bomba: el día que exista un empleado real apellidado Prueba,
-- este script lo borraría sin avisar.
--
-- Uso:
--   docker exec -i chronos-postgres psql -U chronos -d chronos < scripts/limpiar-datos-prueba.sql

BEGIN;

CREATE TEMP TABLE basura_verificacion (correo text PRIMARY KEY) ON COMMIT DROP;

INSERT INTO basura_verificacion (correo) VALUES
    ('prueba.automatica@chronos.mx'),
    ('prueba.183324@chronos.mx'),
    ('prueba.183405@chronos.mx'),
    ('veronica.190132@chronos.mx'),
    ('veronica.190250@chronos.mx');

-- Red de seguridad: solo se tocan cuentas ya dadas de baja. Si alguno de esos correos
-- estuviera activo hoy, significa que ya no es lo que este script cree y se aborta.
DO $$
DECLARE
    activos int;
BEGIN
    SELECT count(*) INTO activos
    FROM empleados e
    JOIN basura_verificacion b ON b.correo = e.correo_corporativo
    WHERE e.activo;

    IF activos > 0 THEN
        RAISE EXCEPTION 'Hay % empleado(s) activo(s) en la lista de borrado. Abortado.', activos;
    END IF;
END $$;

-- Las cuentas de Identity no tienen llave foránea desde empleados, así que se guardan
-- antes de perder la referencia. Sus roles y claims sí caen por cascada.
-- Ojo con el nombre: las tablas de Identity conservan su PascalCase original y exigen
-- comillas, aunque sus columnas sí pasaron por la convención snake_case del proyecto.
CREATE TEMP TABLE usuarios_a_borrar (id uuid PRIMARY KEY) ON COMMIT DROP;

INSERT INTO usuarios_a_borrar (id)
SELECT e.usuario_id
FROM empleados e
JOIN basura_verificacion b ON b.correo = e.correo_corporativo
WHERE e.usuario_id IS NOT NULL;

-- Las checadas y las credenciales WebAuthn se van en cascada con el empleado.
DELETE FROM empleados e
USING basura_verificacion b
WHERE b.correo = e.correo_corporativo;

DELETE FROM "AspNetUsers" u
USING usuarios_a_borrar x
WHERE x.id = u.id;

COMMIT;

SELECT numero_empleado, nombres, apellido_paterno, correo_corporativo, activo
FROM empleados
ORDER BY activo DESC, numero_empleado;
