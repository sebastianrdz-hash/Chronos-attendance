<#
.SYNOPSIS
    Genera los certificados de desarrollo que Vite y Kestrel usan para servir HTTPS.

.DESCRIPTION
    La camara y WebAuthn solo funcionan en contexto seguro. En localhost el navegador
    hace una excepcion, pero al abrir la aplicacion desde un celular contra la IP de la
    red local esa excepcion desaparece y ambas cosas fallan.

    mkcert crea una autoridad certificadora local, la instala en el almacen de confianza
    del sistema y emite un certificado valido para localhost y para la IP de esta maquina.
    Los archivos quedan en certificados/ y no se versionan.

    Nota: los mensajes van sin acentos a proposito. Windows PowerShell 5.1 interpreta los
    .ps1 como ANSI y no como UTF-8, asi que cualquier acento saldria corrupto en consola.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/certificados-dev.ps1
#>

[CmdletBinding()]
param(
    # IP adicional a incluir en el certificado. Si se omite se detecta la de la
    # interfaz que tiene puerta de enlace activa.
    [string]$IpLocal
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz 'certificados'

function Resolver-Mkcert {
    $comando = Get-Command mkcert -ErrorAction SilentlyContinue
    if ($comando) { return $comando.Source }

    # winget reporta un alias, pero la PATH del proceso actual no lo refleja hasta
    # reiniciar la terminal. Se busca primero el enlace y luego el paquete real.
    $candidatos = @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\mkcert.exe')
    )

    $paquete = Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages') `
        -Filter 'mkcert.exe' -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($paquete) { $candidatos += $paquete.FullName }

    foreach ($ruta in $candidatos) {
        if ($ruta -and (Test-Path $ruta)) { return $ruta }
    }

    return $null
}

function Resolver-IpLocal {
    $configuracion = Get-NetIPConfiguration |
        Where-Object { $null -ne $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
        Select-Object -First 1

    if (-not $configuracion) {
        throw 'No hay una interfaz de red activa. Pasa la IP a mano con -IpLocal.'
    }

    return $configuracion.IPv4Address.IPAddress
}

$mkcert = Resolver-Mkcert

if (-not $mkcert) {
    Write-Host 'mkcert no aparece en el sistema. Instalando con winget...' -ForegroundColor Yellow
    winget install --id FiloSottile.mkcert --accept-source-agreements --accept-package-agreements --disable-interactivity | Out-Null
    $mkcert = Resolver-Mkcert
}

if (-not $mkcert) {
    throw 'No se pudo instalar mkcert. Descargalo de https://github.com/FiloSottile/mkcert'
}

if (-not $IpLocal) { $IpLocal = Resolver-IpLocal }

Write-Host "mkcert:   $mkcert"
Write-Host "IP local: $IpLocal"
Write-Host ''

# Registra la autoridad local en el almacen del sistema. Windows pide confirmacion la
# primera vez; en las siguientes no hace nada porque ya quedo registrada.
Write-Host 'Registrando la autoridad certificadora local...' -ForegroundColor Cyan
& $mkcert -install
if ($LASTEXITCODE -ne 0) { throw 'Fallo mkcert -install.' }

New-Item -ItemType Directory -Path $destino -Force | Out-Null

$nombres = @('localhost', '127.0.0.1', '::1', $IpLocal)

# Vite lee el par PEM.
Write-Host ''
Write-Host 'Emitiendo certificado PEM para Vite...' -ForegroundColor Cyan
& $mkcert -cert-file (Join-Path $destino 'chronos-dev.pem') `
          -key-file (Join-Path $destino 'chronos-dev-key.pem') `
          @nombres
if ($LASTEXITCODE -ne 0) { throw 'No se pudo emitir el certificado PEM.' }

# Kestrel carga PKCS#12. mkcert lo protege siempre con la contrasena "changeit".
Write-Host ''
Write-Host 'Emitiendo certificado PKCS#12 para Kestrel...' -ForegroundColor Cyan
& $mkcert -pkcs12 -p12-file (Join-Path $destino 'chronos-dev.pfx') @nombres
if ($LASTEXITCODE -ne 0) { throw 'No se pudo emitir el certificado PKCS#12.' }

# La raiz de la CA se copia junto a los certificados para tenerla a mano al instalarla
# en el celular, que es el unico paso manual de todo el proceso.
$raizCa = Join-Path (& $mkcert -CAROOT) 'rootCA.pem'
if (Test-Path $raizCa) {
    Copy-Item $raizCa (Join-Path $destino 'rootCA.pem') -Force
}

Write-Host ''
Write-Host 'Listo.' -ForegroundColor Green
Write-Host ''
Write-Host 'Desde esta computadora:'
Write-Host '  Cliente   https://localhost:5173'
Write-Host '  API       https://localhost:7080/swagger'
Write-Host ''
Write-Host 'Desde un celular en la misma red WiFi:'
Write-Host "  Cliente   https://${IpLocal}:5173"
Write-Host "  API       https://${IpLocal}:7080/swagger"
Write-Host ''
Write-Host 'El celular necesita confiar en la autoridad local una sola vez:' -ForegroundColor Yellow
Write-Host '  1. Copia certificados\rootCA.pem al celular (correo, Drive o USB).'
Write-Host '  2. Android: Ajustes > Seguridad > Cifrado y credenciales > Instalar certificado'
Write-Host '     > Certificado de CA.'
Write-Host '     iOS: abre el archivo, Ajustes > Perfil descargado > Instalar, y luego'
Write-Host '     Ajustes > General > Info > Ajustes de confianza de certificados, y habilita mkcert.'
Write-Host ''
Write-Host 'Si el celular no conecta, abre los puertos (PowerShell como administrador):' -ForegroundColor Yellow
Write-Host '  New-NetFirewallRule -DisplayName "Chronos dev" -Direction Inbound -Protocol TCP -LocalPort 5173,7080 -Action Allow -Profile Private'
