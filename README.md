# Chronos · Control de asistencia corporativo

Sistema de asistencia con **señales de presencia verificables**: cada fichaje acumula
evidencias independientes y recibe un puntaje de confianza, en lugar de ser un simple
botón que dice «presente».

Construido con ASP.NET Core 10 (Minimal APIs), React 19 + TypeScript y PostgreSQL.

> **Estado:** fase 2 completa. Además del login, el sistema ya administra plantilla,
> departamentos, sedes y turnos, con autorización por rol aplicada en el servidor y una
> interfaz distinta para cada perfil. 229 pruebas automatizadas en verde.

### Demo

| | URL |
|---|---|
| Cliente | https://chronos-attendance-ashy.vercel.app |
| API + Swagger | https://chronos-attendance.onrender.com/swagger |
| Health check | https://chronos-attendance.onrender.com/health/ready |

Las cuentas son las mismas de la semilla (`admin@chronos.mx` / `Chronos#2026`, y las de Supervisor y Empleado). El cliente corre en Vercel y no hiberna; la API está en el plan gratuito de Render y **sí se duerme** tras un rato sin tráfico. Un monitor pega cada 5 minutos a `/health/ready` para mantenerla despierta. Si ese ping falla, la página de login abre al instante y la espera aparece al pulsar **Entrar** (a veces más de un minuto): no es un error de la aplicación.

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
git clone https://github.com/sebastianrdz-hash/Chronos-attendance.git
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

### HTTPS en desarrollo y pruebas desde un celular

La cámara y WebAuthn solo funcionan en **contexto seguro**. El navegador le concede esa
condición a `localhost` aunque vaya por HTTP plano, así que en la propia computadora todo
funciona sin hacer nada. Pero al abrir la aplicación desde un celular contra la IP de la red
local —`http://192.168.x.x:5173`— esa excepción desaparece: `navigator.mediaDevices` llega
`undefined` y el registro de WebAuthn falla. Sin HTTPS, la mitad del fichaje no se puede
probar en un dispositivo real.

Un script resuelve el trámite completo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/certificados-dev.ps1
```

Instala [mkcert](https://github.com/FiloSottile/mkcert) si hace falta, crea una autoridad
certificadora local, la registra en el almacén de confianza de Windows y emite un
certificado válido para `localhost`, `127.0.0.1`, `::1` y la IP de la máquina. Los archivos
quedan en `certificados/`, que no se versiona.

A partir de ahí, Vite y Kestrel lo detectan solos:

| | Desde la computadora | Desde el celular |
|---|---|---|
| Cliente | https://localhost:5173 | `https://<ip-local>:5173` |
| API + Swagger | https://localhost:7080/swagger | `https://<ip-local>:7080/swagger` |

El certificado es **opcional**: si no está, Vite y la API levantan en HTTP como siempre y
solo se pierde el acceso desde el celular. Un clon recién bajado no se rompe por no haberlo
generado.

Para que el celular confíe en la autoridad local hay que instalarla una vez, y es el único
paso manual: copia `certificados/rootCA.pem` al teléfono y en Android ve a *Ajustes >
Seguridad > Cifrado y credenciales > Instalar certificado > Certificado de CA*; en iOS ábrelo,
instálalo desde *Ajustes > Perfil descargado* y actívalo en *Ajustes > General > Información >
Ajustes de confianza de certificados*. Si aun así no conecta, lo más probable es el firewall:

```powershell
New-NetFirewallRule -DisplayName "Chronos dev" -Direction Inbound -Protocol TCP `
  -LocalPort 5173,7080 -Action Allow -Profile Private
```

**Por qué el cliente y la API comparten certificado pero no puerto.** El navegador nunca
habla directo con la API: pide todo a Vite, que reenvía `/api` y `/health` al 5080. Por eso
el origen que ve el navegador —y que WebAuthn usará como *Relying Party ID*— es el de Vite,
no el de la API. El HTTPS del 7080 existe para abrir Swagger desde el celular y para que
nada dependa de por dónde se entre.

#### WebAuthn no funciona sobre una IP, y eso no tiene arreglo

Con HTTPS resuelto, el QR ya se puede probar desde el celular contra `https://192.168.x.x:5173`.
**WebAuthn no.** La norma exige que el *Relying Party ID* sea un nombre de dominio, y una
dirección IP nunca lo es: el navegador responde con `SecurityError` sin más explicación. No
es una limitación de este proyecto ni algo que se pueda configurar; está en la
[especificación](https://www.w3.org/TR/webauthn-3/#relying-party-identifier), que excluye las
IP a propósito por los problemas de identificar servidores por dirección en una PKI.

Las tres salidas, de menos a más trabajo:

| Cómo probar | Qué cubre | Qué cuesta |
| --- | --- | --- |
| Windows Hello en `https://localhost:5173` | El flujo completo: alta, firma y checada verificada | Nada, funciona ya |
| Un túnel a un dominio real (`cloudflared`, `ngrok`) | Todo, incluido el sensor del celular | Un comando y ajustar `WebAuthn__RpId` |
| Un nombre de dominio en la red local con DNS propio | Todo | Montar el DNS |

Lo práctico es lo primero para el día a día y un túnel cuando haga falta ver el sensor del
teléfono. Con `cloudflared` no hace falta ni registrarse:

```powershell
cloudflared tunnel --url https://localhost:5173 --no-tls-verify
```

Devuelve una URL tipo `https://algo-aleatorio.trycloudflare.com`. Para que el enrolamiento la
acepte hay que decírselo a la API antes de levantarla, porque el RP ID debe coincidir con lo
que ve el navegador:

```powershell
$env:WebAuthn__RpId = "algo-aleatorio.trycloudflare.com"
$env:WebAuthn__OrigenesPermitidos__0 = "https://algo-aleatorio.trycloudflare.com"
dotnet run --project src/Chronos.Api
```

Las credenciales quedan atadas al dominio con el que se registraron: al cambiar de túnel hay
que volver a enrolar. Es el comportamiento correcto —una credencial de un dominio no vale en
otro—, pero conviene saberlo antes de pensar que algo se rompió.

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
├── scripts/
│   └── certificados-dev.ps1         Emite los certificados de HTTPS para desarrollo.
├── certificados/                    Generado por el script; no se versiona.
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
- **El QR prueba presencia frente a una pantalla, no identidad.** Ver el código no dice
  quién lo vio. Por eso vale 25 de 100 y jamás alcanza solo el umbral.
- **WebAuthn es la señal fuerte** porque une el dispositivo físico con un gesto biométrico,
  y su firma no se puede replicar sin la llave privada del enclave.

Ninguna señal es suficiente sola. Ese es exactamente el punto del diseño.

### Cómo se defiende el fichaje por QR

El sentido del flujo es la mitad de la defensa: **la sede muestra el código y el empleado lo
escanea**. Al revés —que cada empleado mostrara el suyo— bastaría con mandar una captura por
mensajería para que otro fichara por él.

Sobre esa base se apilan tres controles, cada uno tapando el hueco del anterior:

| Ataque | Qué lo detiene |
| --- | --- |
| Fabricar un código desde cero | Va firmado con HMAC-SHA256 y una llave que solo conoce el servidor |
| Fotografiar el código y reenviarlo por mensajería | Caduca en 30 segundos |
| Reenviarlo **dentro** de esos 30 segundos | Cada código lleva un *nonce* de un solo uso: el segundo intento se rechaza |
| Presentar un código de otra sede | El servidor compara la sede del token contra la del expediente |
| Quedarse frente al kiosco fichando en bucle | Ventana antiduplicados de 5 minutos por tipo de fichaje |

**Lo que sigue sin resolver, dicho con honestidad:** un empleado puede pedirle a un compañero
que escanee por él estando este último físicamente en la sede, y nada de lo anterior lo
impide. El QR demuestra que *alguien con una sesión válida estuvo frente a la pantalla*, no
que fuera el titular de esa sesión. Cerrar ese hueco es justamente el trabajo de WebAuthn, que
exige el gesto biométrico en el dispositivo enrolado del propio empleado.

Por eso un fichaje solo con QR se registra con 25 puntos y estado **«pendiente de revisión»**:
el sistema lo acepta y lo deja anotado, pero no lo da por bueno hasta que Recursos Humanos lo
confirme o una segunda señal lo respalde.

### Cómo se defiende el fichaje por WebAuthn

WebAuthn cierra el hueco que el QR deja abierto: prueba **quién** ficha, no solo que alguien
estuvo frente a la pantalla. Al escanear, el navegador pide confirmar con huella, rostro o
PIN, y el dispositivo firma un desafío del servidor con una llave privada que vive en su
enclave seguro.

**De la biometría no se guarda absolutamente nada.** El sensor no entrega la huella a la
página: solo desbloquea la llave. Al servidor llegan la clave pública y un contador de firmas,
y eso es todo lo que hay en la base. Una filtración completa de `credenciales_webauthn` no
expondría ningún rasgo de nadie, porque no está ahí.

| Ataque | Qué lo detiene |
| --- | --- |
| Robar la base de datos y suplantar a alguien | Solo hay claves públicas; firmar exige la privada, que nunca salió del dispositivo |
| Copiar una firma anterior y reenviarla | Cada ceremonia lleva un desafío nuevo, de un solo uso y con 2 minutos de vigencia |
| Montar una página falsa que pida la firma | El origen va dentro de lo firmado; una firma emitida para otro dominio no verifica |
| Clonar la llave del dispositivo | El contador de firmas retrocedería, y el servidor lo detecta |
| Canjear un desafío de alta como si fuera de fichaje | Los desafíos llevan propósito y no son intercambiables |
| Usar una credencial ya revocada | La búsqueda exige que siga activa y ligada al empleado de la sesión |

Sumado al QR, un fichaje llega a 70 puntos y queda **verificado** sin intervención humana.
Defraudar exigiría estar físicamente frente al kiosco *y* tener el dispositivo desbloqueado
del titular, que es justo el nivel de esfuerzo que se buscaba imponer.

**Lo que sigue sin resolver:** un empleado puede prestar su teléfono desbloqueado a un
compañero, o registrar su huella y la de otra persona en el mismo aparato. Ninguna solución
por software lo evita; solo un control presencial lo haría.

#### Configuración del Relying Party

Es la parte que más errores confusos provoca. El `RpId` y los orígenes describen lo que ve el
**navegador** —el host de Vite—, no el puerto de la API:

```jsonc
"WebAuthn": {
  "RpId": "localhost",                              // dominio, sin esquema ni puerto
  "NombreRp": "Chronos",                            // lo que muestra el diálogo del sistema
  "OrigenesPermitidos": ["https://localhost:5173"], // con esquema y puerto
  "SegundosVigenciaDesafio": 120
}
```

En otros entornos se sobrescribe con `WebAuthn__RpId` y `WebAuthn__OrigenesPermitidos__0`.

### Qué pasa con lo que el sistema no puede decidir

El umbral de confianza deja un residuo por diseño: fichajes que reunieron evidencia, pero no
la suficiente. Descartarlos castigaría a quien sí trabajó; aceptarlos volvería decorativo el
umbral. Chronos hace lo tercero: los registra, los marca y los manda a una bandeja donde una
persona decide.

- La bandeja (`/revision`) muestra cada checada con las señales que aportó y lo que le faltó,
  para que el dictamen no dependa de adivinar qué significa «25 de 100».
- **Nadie dictamina su propia checada, ni el administrador.** Todo el modelo se apoya en que
  la evidencia débil la valide alguien distinto de quien la generó; la autoaprobación
  convertiría la revisión en un trámite. La regla vive en `PoliticaAcceso.PuedeRevisarChecada`
  y la bandeja ni siquiera ofrece los casos propios.
- Un supervisor solo ve y resuelve lo de su departamento. La lista se filtra con el mismo
  criterio con que el servidor autoriza, para que no aparezcan casos que al pulsar darían 403.
- Aprobar y rechazar exigen un motivo escrito. **Rechazar no borra la checada**: deja de
  contar para la jornada pero sigue ahí, con quién lo decidió y por qué, porque el registro de
  lo que se descartó importa tanto como el de lo que se aceptó.
- Un dictamen no se pisa: la segunda persona que intente resolver el mismo caso recibe un 409
  y una remisión a la bitácora.

### La bitácora es inmutable de verdad

Cada dictamen deja un asiento con la acción, quién, cuándo, por qué, el estado del que se
venía y el puntaje que tenía la checada en ese momento. El asiento y el cambio de estado se
guardan en la misma transacción: no puede quedar una checada resuelta sin rastro, ni un rastro
de algo que no ocurrió.

La parte que la hace creíble es que **la inmutabilidad no depende de la aplicación**. Un
disparador de PostgreSQL rechaza cualquier `UPDATE` o `DELETE` sobre `bitacora`:

```sql
CREATE TRIGGER bitacora_sin_modificaciones
BEFORE UPDATE OR DELETE ON bitacora
FOR EACH ROW EXECUTE FUNCTION bitacora_es_inmutable();
```

Que el código no toque los asientos es fácil de garantizar hoy y fácil de romper dentro de un
año. El disparador protege también contra un `psql` con prisa, y hay una prueba de integración
que intenta reescribir un asiento desde EF Core y comprueba que la base lo impide. Sin eso,
«solo inserción» sería una nota en el README.

### El cambio de horario, dicho explícitamente

Dos noches al año el turno nocturno no dura lo que marca el reloj de pared. La decisión es:

- El **horario** se pacta en hora local. Un turno de 22:00 a 06:00 empieza a las 22:00 aunque
  esa noche solo tenga siete horas, y quien se va a las 06:00 no es «salida anticipada».
- Las **horas trabajadas** se miden en tiempo real transcurrido. La noche que adelanta se
  pagan siete horas; la que atrasa genera una hora extra. Es lo que de verdad pasó.
- La hora que **no existe** (02:30 el día que adelanta) se recorre a la siguiente válida. La
  que ocurre **dos veces** se resuelve a la primera pasada, así que quien llega en la segunda
  cuenta con una hora de retardo. La regla es discutible; lo importante es que sea explícita
  y no un accidente del framework.

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

Son 229: **91 unitarias** de dominio —reglas de jornada, evaluación de confianza, firma de
los códigos QR y políticas de acceso— y **138 de integración** contra la API.

El cambio de horario de verano tiene su propio archivo de pruebas
(`JornadaEnCambioDeHorarioTests`) porque es donde la aritmética ingenua de horas se rompe.
Se apoya en una zona horaria construida a mano: México abolió el horario de verano en 2022,
así que ninguna zona mexicana sirve ya para ejercitar el salto, y amarrar las pruebas a
`America/Los_Angeles` las volvería rehenes de las actualizaciones de husos del sistema
operativo. Cubren la noche que dura siete horas, la que dura nueve, la hora que no existe y
la que ocurre dos veces.

WebAuthn se prueba de verdad, sin hardware. `AutenticadorDeSoftware` implementa un
autenticador FIDO2 completo: genera una clave P-256, arma el `authData`, codifica la clave
pública en COSE y firma los desafíos. Lo que la API valida es criptografía auténtica; lo
único simulado es el sensor. Eso permite cubrir en CI lo que de otro modo exigiría un dedo
sobre un lector: firmas de otra clave, orígenes suplantados, contadores que no avanzan,
desafíos reutilizados y credenciales revocadas.

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
- [x] Semilla realista y suite ampliada de pruebas

**Fase 3 — en curso**

- [x] Contexto seguro en desarrollo: HTTPS para el cliente y la API, con certificado
      válido también para la IP de red local
- [x] Fichaje por QR: kiosco con código rotativo firmado, escáner en el cliente y
      validación de firma, vigencia, sede y nonce de un solo uso
- [x] Registro y verificación WebAuthn con Fido2.NET: alta de varios dispositivos por
      empleado, revocación y firma al fichar que lleva la checada a confianza alta
- [x] Bandeja de revisión de RH: aprobar o rechazar checadas débiles dejando constancia,
      con bitácora inmutable protegida por un disparador de PostgreSQL
- [x] Cálculo de jornada expuesto en la pantalla de asistencia del día y en el panel,
      con pruebas del turno nocturno y de los dos cambios de horario de verano
- [ ] Exportación a Excel con ClosedXML

**Fase 4 — pendiente**

- [ ] App móvil en .NET MAUI con Shiny.Beacons para monitoreo de regiones iBeacon

---

## Despliegue

La demo pública usa el mismo esquema que [Prueba-Gastos](https://github.com/sebastianrdz-hash/Prueba-Gastos): **Neon** (PostgreSQL), **Render** (API en Docker) y **Vercel** (SPA). GitHub Pages no sirve: no ejecuta ASP.NET ni hospeda Postgres.

| | URL |
|---|---|
| Cliente | https://chronos-attendance-ashy.vercel.app |
| API | https://chronos-attendance.onrender.com |
| Health check | https://chronos-attendance.onrender.com/health/ready |

El `Dockerfile` de la raíz empaqueta la API. El cliente se publica desde `Frontend/` con `vercel.json` (rewrites del enrutador). La cadena que da Neon llega como URI (`postgresql://…`); Npgsql solo entiende pares `Host=…`, así que `CadenaDeConexion` la traduce al arrancar. El puerto lo publica Render en `PORT`.

**Por qué a veces tarda en abrir.** Vercel sirve archivos estáticos y responde enseguida. Render Free apaga el contenedor cuando no hay peticiones; Neon también puede suspender el compute. El primer llamado a la API tras ese silencio arranca ambas cosas y puede tardar un minuto. Un monitor HTTP cada 5 minutos a `/health/ready` mantiene el proceso despierto la mayor parte del tiempo; si el monitor se cae, el síntoma es un login lento, no una pantalla rota.

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
