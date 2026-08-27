# Chronos · Control de asistencia corporativo

Sistema de asistencia con **señales de presencia verificables**: cada fichaje acumula
evidencias independientes y recibe un puntaje de confianza, en lugar de ser un simple
botón que dice «presente».

Construido con ASP.NET Core 10 (Minimal APIs), React 19 + TypeScript y PostgreSQL.

> **Estado:** fase 2 completa. Además del login, el sistema ya administra plantilla,
> departamentos, sedes y turnos, con autorización por rol aplicada en el servidor y una
> interfaz distinta para cada perfil. 125 pruebas automatizadas en verde.

---

## La idea central: una checada no es un booleano

Casi todos los sistemas de asistencia responden a una sola pregunta: *¿marcó o no marcó?*
Eso los vuelve fáciles de burlar, porque basta con vencer un único control (prestar una
credencial, compartir una foto del QR, marcar por un compañero).

Chronos modela el problema distinto. Un fichaje es un **momento respaldado por señales**, y
cada señal prueba algo diferente:

| Señal | Peso | Qué prueba realmente |
|---|---:|---|
| **WebAuthn** | 45 | Que es el dispositivo del empleado y que su biometría lo desbloqueó |
| **Código QR** | 25 | Que quien ficha tenía a la vista la credencial vigente |
| **Beacon BLE** | 20 | Que el dispositivo está físicamente dentro de la zona |
| **Registro manual** | 30 | Que un supervisor respaldó el fichaje de forma explícita |
| **Geocerca** | 10 | Que las coordenadas caen dentro del perímetro de la sede |
| **Red WiFi** | 10 | Que el dispositivo está en la red corporativa |

Las señales confirmadas suman una sola vez por tipo; las fallidas y las sospechosas
descuentan. El puntaje resultante clasifica la checada:

- **≥ 70 puntos** → verificada
- **40 – 69** → se acepta pero se marca para revisión de Recursos Humanos
- **1 – 39** → confianza baja, revisión obligatoria
- **0** → rechazada, no cuenta para el cálculo de jornada

Los pesos están calibrados para que el flujo completo de la fase 1 (QR + WebAuthn = 70)
alcance justo el umbral alto. Defraudar el sistema exige vencer varios controles a la vez,
y lo que llega débil queda visible en lugar de esconderse.

La política vigente se consulta en `GET /api/v1/meta/politica-confianza`, de modo que la
interfaz explica sus decisiones sin duplicar los umbrales.

### Por qué el esquema ya contempla los beacons

Aunque en la fase 1 solo se llenan QR y WebAuthn, la tabla `senales_presencia` existe desde
la primera migración con una columna `detalle_json` de tipo **jsonb**. Cada tipo de señal
guarda ahí su carga propia:

```jsonc
// Beacon BLE
{ "uuid": "f7826da6-4fa2-4e98-8024-bc5b71e0893e", "major": 1, "minor": 42, "rssi": -67 }
// Código QR
{ "folio": "QR-8891" }
```

Sumar un tipo nuevo es agregar un valor al enum y una forma de JSON, no rediseñar el
esquema. Hay una prueba de integración que lo verifica insertando una señal de beacon y
consultándola con el operador `->>` de PostgreSQL.

---

## Arranque rápido

**Requisitos:** [.NET SDK 10](https://dotnet.microsoft.com/download),
[Node 20+](https://nodejs.org) y [Docker](https://www.docker.com/products/docker-desktop).

```bash
git clone https://github.com/<tu-usuario>/chronos-attendance.git
cd chronos-attendance
cp .env.example .env          # en PowerShell: Copy-Item .env.example .env

# 1. Base de datos (PostgreSQL en el 5433 + Adminer en el 8080)
docker compose up -d

# 2. API — aplica migraciones y siembra datos al arrancar
dotnet run --project Backend/src/Chronos.Api

# 3. Cliente, en otra terminal
cd Frontend && npm install && npm run dev
```

| Servicio | URL |
|---|---|
| Cliente React | http://localhost:5173 |
| API + Swagger | http://localhost:5080/swagger |
| Adminer | http://localhost:8080 |
| Health check | http://localhost:5080/health/ready |

### Cuentas sembradas

| Rol | Correo | Contraseña |
|---|---|---|
| Admin | `admin@chronos.mx` | `Chronos#2026` |
| Supervisor | `supervisor@chronos.mx` | `Chronos#2026` |
| Empleado | `empleado@chronos.mx` | `Chronos#2026` |

Cada una entra a una interfaz distinta. La semilla completa deja 2 sedes, 4 departamentos,
3 turnos —uno nocturno que cruza la medianoche— y 15 empleados repartidos, uno de ellos
dado de baja para que se vea el filtro de bajas.

> El puerto de PostgreSQL es el **5433**, no el 5432, para no chocar con una instalación
> nativa de PostgreSQL en la misma máquina. Se cambia con `POSTGRES_PORT` en el `.env`.

Para configurar el entorno a mano, copia `.env.example` como `.env` y ajusta los valores.

---

## Roles: quién puede hacer qué

La autorización se aplica **en el servidor**. La interfaz oculta lo que no corresponde, pero
esa es una comodidad, no la defensa: cada endpoint vuelve a preguntar.

| | Admin | Supervisor | Empleado |
|---|:---:|:---:|:---:|
| Ver la plantilla completa | sí | sí | no |
| Ver su propio expediente | sí | sí | sí |
| Dar de alta, editar y dar de baja empleados | toda la organización | solo su departamento | no |
| Asignar rol de Supervisor o Admin | sí | no | no |
| Mover a alguien de departamento o sede | sí | no | no |
| Administrar sedes y turnos | sí | solo lectura | no |
| Editar un departamento | cualquiera | solo el suyo | no |
| Cambiar su propia contraseña | sí | sí | sí |

Las reglas viven en `PoliticaAcceso`, una clase estática de `Chronos.Domain` sin
dependencias de ASP.NET. Recibe un `ContextoAcceso` (rol, empleado, departamento y sede del
solicitante) y devuelve un `ResultadoAcceso` que dice si se permite y, cuando no, por qué.
Eso permite probar la autorización como lo que es —una regla de negocio— y devolver un 403
con un motivo legible en lugar de un rechazo mudo.

El `ContextoAcceso` no se arma solo con los claims del token: `ResolutorAcceso` relee de la
base el departamento y la sede vigentes del solicitante. Si a un supervisor lo cambian de
área, sus permisos cambian en la siguiente petición y no cuando expire su token.

### Alta de empleado y contraseña temporal

Dar de alta a alguien crea, dentro de una misma transacción, el expediente y su cuenta de
Identity con el rol correspondiente. La API devuelve una contraseña temporal **una sola
vez**: no se almacena en claro ni se puede volver a consultar; si se pierde, queda reiniciar
el acceso.

La cuenta nace marcada con `DebeCambiarContrasena`, y mientras esa marca esté encendida el
guardián de rutas empuja a la pantalla de cambio sin importar la URL que se escriba. El
motivo es que quien dio de alta al empleado conoce esa contraseña: hasta que la cambie,
nadie puede afirmar que las checadas hechas con su cuenta son suyas.

Los empleados nunca se borran, se dan de baja. Un borrado físico se llevaría por delante su
historial de checadas, que es justo lo que un sistema de asistencia debe conservar.

---

## Arquitectura

```
chronos-attendance/
├── Backend/
│   ├── Chronos.sln
│   ├── Directory.Build.props        Propiedades comunes de todos los proyectos .NET.
│   ├── src/
│   │   ├── Chronos.Domain/          Entidades, reglas de jornada y políticas de acceso puras.
│   │   ├── Chronos.Infrastructure/  EF Core + PostgreSQL, Identity, emisión de JWT.
│   │   └── Chronos.Api/             Minimal APIs, CRUD, autorización, OpenAPI, health checks.
│   └── tests/
│       ├── Chronos.Domain.Tests/    Pruebas unitarias, sin base de datos.
│       └── Chronos.Api.Tests/       Pruebas de integración con Testcontainers.
├── Frontend/                        React 19 + TypeScript + Vite + Tailwind + shadcn/ui.
├── docker-compose.yml               PostgreSQL 18 + Adminer.
├── .env.example                     Plantilla de configuración (el .env real no se versiona).
├── .dockerignore
└── .github/workflows/ci.yml         Build, pruebas y verificación de migraciones.
```

Las dependencias apuntan hacia adentro: `Api → Infrastructure → Domain`. El dominio no
conoce a EF Core ni a ASP.NET, y por eso las reglas de negocio se prueban en milisegundos
sin levantar nada.

### Decisiones que vale la pena explicar

**Las reglas de jornada son funciones puras.** `CalculadoraJornada.Calcular` recibe el día,
el turno, la zona horaria y las checadas, y devuelve un resumen. No consulta la base ni lee
el reloj del sistema, así que casos como el turno nocturno que cruza la medianoche o los
saltos de horario de verano se prueban de forma determinista.

**El descanso programado se descuenta aunque nadie lo fiche.** Sin esta regla, una jornada
normal de 9:00 a 18:00 aparecería con una hora extra falsa. Si el descanso registrado fue
mayor al del turno, manda el registrado.

**La configuración se resuelve de forma diferida.** Las opciones de JWT y la cadena de
conexión se leen desde el contenedor de servicios, no al registrar los servicios. Leerlas de
forma anticipada dejaría fuera las fuentes que se agregan después —variables de entorno de
un contenedor, overrides de las pruebas— y la API podría terminar firmando con una llave y
validando con otra.

**Los claims usan nombres cortos al estilo OIDC** (`sub`, `role`) y tanto Identity como la
validación del token están configurados para leerlos, en lugar de los URIs largos de
`ClaimTypes`.

**Los errores de validación se muestran junto al campo que los provocó.** La API responde
con `ProblemDetails` de RFC 9457 y su diccionario `errors`; el cliente lo convierte en un
`ErrorValidacion` y lo inyecta en react-hook-form. Así una colisión de número de empleado
—que solo la base puede detectar— aparece bajo su input y no como un aviso suelto.

**Los parámetros de paginación son nulables.** Minimal APIs trata una propiedad `int` de un
`[AsParameters]` como obligatoria e ignora su inicializador: pedir la lista sin `?pagina=`
reventaba con un 500. `ParametrosConsulta` los declara nulables y los normaliza después, con
sus valores por omisión y el tamaño de página acotado.

**Nomenclatura en español para el dominio.** `Empleado`, `Checada`, `SenalPresencia`: es el
lenguaje ubicuo del negocio. Lo técnico (`DbContext`, hooks de React, `useAuth`) conserva la
convención de cada framework.

**PostgreSQL en snake_case.** Vía `EFCore.NamingConventions`, para que el esquema se lea
idiomático desde Adminer o `psql`.

---

## Privacidad y cumplimiento

Los datos biométricos son **datos personales sensibles** bajo la Ley Federal de Protección
de Datos Personales en Posesión de los Particulares (LFPDPPP) en México. Tratarlos implica
consentimiento expreso y por escrito, y eleva de forma considerable la responsabilidad ante
una brecha.

Chronos evita el problema en la raíz: **usa WebAuthn/FIDO2, no almacena biometría**.

- La huella o el rostro nunca salen del enclave seguro del dispositivo.
- El servidor guarda únicamente una **clave pública**, un identificador de credencial y un
  contador de firmas (tabla `credenciales_web_authn`).
- Una filtración de la base de datos no expone ningún rasgo biométrico: una clave pública,
  por definición, es pública.
- El contador de firmas permite detectar credenciales clonadas: si no crece entre usos, algo
  anda mal.

### Qué garantiza y qué no cada señal

Ser explícito sobre los límites es parte del diseño:

- **Un iBeacon emite su UUID en abierto y es clonable.** Cualquiera con un teléfono puede
  leerlo y retransmitirlo. Por eso el beacon **corrobora** presencia, no la prueba: aporta
  20 de 100 puntos y nunca alcanza por sí solo el umbral de confianza alta.
- **La geocerca es una verificación gruesa.** El GPS se puede falsear con herramientas al
  alcance de cualquiera; sirve para descartar fichajes obviamente fuera de lugar, no para
  confirmar los válidos.
- **El QR prueba posesión de la credencial, no identidad.** Se puede fotografiar y reenviar.
- **WebAuthn es la señal fuerte** porque une el dispositivo físico con un gesto biométrico,
  y su firma no se puede replicar sin la llave privada del enclave.

Ninguna señal es suficiente sola. Ese es exactamente el punto del diseño.

---

## Restricciones técnicas conocidas

Investigadas antes de diseñar, para no prometer lo que la plataforma no da:

- **Safari en iOS no soporta Web Bluetooth**, en ninguna versión. No es un problema de
  permisos ni de HTTPS: la API sencillamente no existe. Por eso la detección de beacons
  exige una aplicación nativa y no se intenta desde el navegador.
- **Android 8+ limita el escaneo BLE en segundo plano.** Los límites de ejecución en
  background restringen los escaneos prolongados salvo que la app corra un *foreground
  service* con notificación persistente visible para el usuario.
- **Guardar el token en `localStorage` es una decisión de prototipo.** Es vulnerable a XSS.
  Un despliegue productivo debería usar un refresh token en cookie `httpOnly` y mantener el
  access token solo en memoria. Está anotado como deuda técnica consciente.

---

## Pruebas

```bash
cd Backend
dotnet test                                  # toda la suite
dotnet test tests/Chronos.Domain.Tests       # unitarias, sin Docker
dotnet test tests/Chronos.Api.Tests          # de integración, requieren Docker
```

Son 125: **59 unitarias** de dominio —reglas de jornada, evaluación de confianza y políticas
de acceso— y **66 de integración** contra la API.

Las de integración levantan un PostgreSQL efímero con **Testcontainers**, usando la misma
imagen que `docker-compose`, y aplican las migraciones reales. Una de ellas verifica que las
pruebas apunten al contenedor y no a la base de desarrollo, para que no puedan pasar por
accidente contra datos locales.

`AutorizacionTests` recorre los casos que dan sentido a la fase: un supervisor no edita
empleados de otro departamento, no promueve a nadie a Admin, no mueve gente entre áreas y no
administra catálogos; un empleado no lista a nadie más que a sí mismo; un administrador no
puede darse de baja a sí mismo.

El CI de GitHub Actions corre tres trabajos: backend, cliente y un tercero que aplica las
migraciones sobre una base vacía y falla si el modelo tiene cambios sin migrar.

---

## Hoja de ruta

**Fase 1 — completa**

- [x] Solución en capas con seis proyectos
- [x] PostgreSQL + Adminer en Docker
- [x] Modelo de dominio con señales de presencia desde la primera migración
- [x] Identity + JWT con roles Admin, Supervisor y Empleado
- [x] Login funcional de punta a punta
- [x] OpenAPI, health checks y Serilog
- [x] CI y suite de pruebas

**Fase 2 — completa**

- [x] CRUD de empleados, departamentos, sedes y turnos con paginación, búsqueda y orden
- [x] Alta de empleado que crea su cuenta de Identity con contraseña temporal
- [x] Autorización por rol aplicada en el servidor, con pruebas que la verifican
- [x] Layout y navegación por rol, rutas protegidas y tres paneles distintos
- [x] Formularios con react-hook-form + zod que muestran los errores de la API
- [x] Perfil propio con turno asignado y cambio de contraseña
- [x] Semilla realista y suite ampliada a 125 pruebas

**Fase 3 — siguiente**

- [ ] Fichaje por QR (QRCoder + ZXing.Net + html5-qrcode)
- [ ] Registro y verificación WebAuthn con Fido2.NET
- [ ] Cálculo de jornada expuesto en reportes y dashboard
- [ ] Exportación a Excel con ClosedXML y bitácora de auditoría
- [ ] App móvil en .NET MAUI con Shiny.Beacons para monitoreo de regiones iBeacon

---

## Contexto

Chronos es la evolución profesional de
[tumlee-qr-system](https://github.com/sebastianrdz-hash/tumlee-qr-system), un prototipo
front-end de asistencia universitaria por QR. Mismo dominio, pero con backend real, base de
datos, autenticación seria y un modelo de confianza explícito. No comparte código: comparte
el conocimiento del problema.

---

## Licencia

[MIT](LICENSE) © 2026 Sebastian Méndez Rodríguez.

Las credenciales que aparecen en este repositorio —la llave JWT de
`appsettings.Development.json`, la contraseña de la semilla y la de PostgreSQL en
`docker-compose.yml`— son valores de desarrollo local, pensados para que un clon arranque
sin configurar nada. No sirven en ningún otro entorno: ahí se sobrescriben con variables
como `Jwt__Llave` y `ConnectionStrings__Postgres`.
