param(
    [Parameter(Mandatory)] [uri] $BaseUrl,
    [Parameter(Mandatory)] [string] $Correo,
    [securestring] $Contrasena = (Read-Host 'Contraseña del usuario de prueba' -AsSecureString)
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.AbsoluteUri.TrimEnd('/')
if ($BaseUrl.Scheme -ne 'https' -and -not $BaseUrl.IsLoopback) {
    throw 'La API pública debe probarse por HTTPS.'
}

function Invoke-Api {
    param(
        [string] $Metodo, [string] $Ruta, [int] $Estado,
        [hashtable] $Datos, [hashtable] $Cabeceras
    )
    $peticion = @{
        Uri = "$base$Ruta"
        Method = $Metodo
        SkipHttpErrorCheck = $true
        MaximumRedirection = 0
        TimeoutSec = 30
    }
    if ($Datos) {
        $peticion.ContentType = 'application/json; charset=utf-8'
        $peticion.Body = $Datos | ConvertTo-Json -Compress
    }
    if ($Cabeceras) { $peticion.Headers = $Cabeceras }
    $respuesta = Invoke-WebRequest @peticion
    if ([int] $respuesta.StatusCode -ne $Estado) {
        throw "$Metodo $Ruta devolvió $($respuesta.StatusCode). Se esperaba $Estado."
    }
    Write-Host "$Metodo $Ruta → $Estado"
    return $respuesta
}

$null = Invoke-Api GET '/swagger/index.html' 200
$openApi = (Invoke-Api GET '/openapi/v1.json' 200).Content | ConvertFrom-Json -AsHashtable
if (-not $openApi.paths['/api/libros'].post.security -or
    $openApi.components.securitySchemes.Bearer.scheme -ne 'bearer') {
    throw 'Swagger no especifica el token Bearer para crear libros.'
}

$claveIncorrecta = [guid]::NewGuid().ToString()
$null = Invoke-Api POST '/api/auth/login' 401 @{ correo = $Correo; contrasena = $claveIncorrecta }
$clave = [pscredential]::new($Correo, $Contrasena).GetNetworkCredential().Password
$login = (Invoke-Api POST '/api/auth/login' 200 @{ correo = $Correo; contrasena = $clave }).Content | ConvertFrom-Json
if (-not $login.accessToken -or $login.tokenType -ne 'Bearer') { throw 'El inicio de sesión no devolvió un token Bearer.' }
$cabeceras = @{ Authorization = "Bearer $($login.accessToken)" }
$datos = @{
    titulo = "Crónicas de prueba $([guid]::NewGuid().ToString('N'))"
    autor = 'María de prueba'
    anioPublicacion = 2022
    existencias = 3
}

$null = Invoke-Api POST '/api/libros' 401 $datos
$null = Invoke-Api POST '/api/libros' 400 @{ titulo = 'Incompleto' } $cabeceras
$ruta = $null
try {
    $alta = Invoke-Api POST '/api/libros' 201 $datos $cabeceras
    $libro = $alta.Content | ConvertFrom-Json
    if ($libro.id -le 0) { throw 'La creación no devolvió un identificador.' }
    $ruta = "/api/libros/$($libro.id)"
    if ([uri]::new($BaseUrl, [string] $alta.Headers.Location[0]).AbsolutePath -ne $ruta) {
        throw 'La cabecera Location apunta a una ruta diferente.'
    }

    $detalle = (Invoke-Api GET $ruta 200).Content | ConvertFrom-Json
    if ($detalle.titulo -cne $datos.titulo -or $detalle.autor -cne $datos.autor -or
        $detalle.existencias -ne $datos.existencias) {
        throw 'El registro consultado difiere del registro creado.'
    }
    $listado = (Invoke-Api GET '/api/libros' 200).Content | ConvertFrom-Json
    if ($libro.id -notin $listado.id) { throw 'El libro creado no aparece en el listado.' }

    $datos.existencias = 8
    $null = Invoke-Api PUT $ruta 200 $datos $cabeceras
    $actualizado = (Invoke-Api GET $ruta 200).Content | ConvertFrom-Json
    if ($actualizado.existencias -ne 8) { throw 'El cambio no quedó guardado.' }

    $null = Invoke-Api DELETE $ruta 204 -Cabeceras $cabeceras
    $rutaEliminada = $ruta
    $ruta = $null
    $null = Invoke-Api GET $rutaEliminada 404
    $listadoFinal = (Invoke-Api GET '/api/libros' 200).Content | ConvertFrom-Json
    if ($libro.id -in $listadoFinal.id) { throw 'El libro eliminado sigue en el listado.' }
    $null = Invoke-Api PUT $rutaEliminada 404 $datos $cabeceras
    $null = Invoke-Api DELETE $rutaEliminada 404 -Cabeceras $cabeceras
}
finally {
    if ($ruta) {
        $null = Invoke-WebRequest -Method DELETE -Uri "$base$ruta" -Headers $cabeceras -SkipHttpErrorCheck -TimeoutSec 15
    }
}

Write-Host 'Verificación completada.'
