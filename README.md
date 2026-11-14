# API REST de inventario de libros

API en C# con ASP.NET Core .NET 10, Entity Framework Core y SQL Server. Swagger UI está disponible en `/swagger` y la especificación OpenAPI en `/openapi/v1.json`.

[Panel de inventario publicado](https://www.inventario-libros-sebas.somee.com/) · [Swagger](https://www.inventario-libros-sebas.somee.com/swagger/index.html)

## Endpoints

| Funcionalidad | Método | Ruta | Cuerpo JSON | Respuesta exitosa |
| --- | --- | --- | --- | --- |
| Iniciar sesión | POST | `/api/auth/login` | `{"correo":"evaluacion@example.test","contrasena":"CLAVE_DE_PRUEBA"}` | `200 OK`, token de acceso |
| Agregar libro | POST | `/api/libros` | `{"titulo":"El principito","autor":"Antoine de Saint-Exupéry","anioPublicacion":1943,"existencias":3}` | `201 Created`, libro creado y cabecera `Location` |
| Actualizar libro | PUT | `/api/libros/{id}` | `{"titulo":"El principito","autor":"Antoine de Saint-Exupéry","anioPublicacion":1943,"existencias":8}` | `200 OK`, libro actualizado |
| Eliminar libro | DELETE | `/api/libros/{id}` | — | `204 No Content` |
| Consultar todos los libros | GET | `/api/libros` | — | `200 OK`, arreglo JSON ordenado por identificador |
| Consultar un libro | GET | `/api/libros/{id}` | — | `200 OK`, libro solicitado |

Las consultas son públicas. Para agregar, actualizar o eliminar, haga el login y envíe `Authorization: Bearer <accessToken>`. En Swagger, pulse **Authorize** y pegue únicamente el valor de `accessToken`. El token vence en una hora. Sustituya `{id}` por el identificador que devolvió la creación y use `Content-Type: application/json` al enviar un cuerpo.

La API responde `400` ante un cuerpo inválido, `401` ante credenciales o token inválidos, `404` si el libro solicitado no existe y `429` si se supera el límite de 10 solicitudes de login por minuto desde una IP.

## Configuración local

1. Instale .NET SDK 10 y tenga disponible una instancia de SQL Server. Cree una base vacía llamada `InventarioLibrosApi` y ejecute [el script de esquema](sql/01-esquema.sql) en esa base.
2. Desde la carpeta del proyecto, configure la conexión. Ejemplo con autenticación integrada de Windows para SQL Server local

   ```powershell
   $env:ConnectionStrings__Inventario = 'Server=.;Database=InventarioLibrosApi;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
   ```

3. Cree un usuario de evaluación con el siguiente comando. La contraseña se guarda como hash mediante ASP.NET Core Identity.

   ```powershell
   $env:INVENTARIO_USUARIO_CORREO = 'evaluacion@example.test'
   $clave = Read-Host 'Contraseña de evaluación (12 a 128 caracteres)' -AsSecureString
   $env:INVENTARIO_USUARIO_CONTRASENA = [pscredential]::new($env:INVENTARIO_USUARIO_CORREO, $clave).GetNetworkCredential().Password
   try {
       dotnet run -- crear-usuario
   }
   finally {
       Remove-Item Env:INVENTARIO_USUARIO_CONTRASENA
   }
   ```

4. Inicie la aplicación y abra `http://localhost:5189/swagger`.

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   dotnet run -- --urls http://localhost:5189
   ```

## Publicación en Somee

1. Cree el sitio en la cuenta de Somee que vaya a usarse para la entrega. Active HTTPS para el subdominio y compruebe en el panel la disponibilidad del runtime ASP.NET Core 10. [Guía de SSL de Somee](https://www.somee.com/doka/Help/Article/171/How_do_I_enable_SSL_for_my_website%3F)
2. Cree una base SQL Server en la misma cuenta. Conéctese con SSMS, seleccione la base asignada y ejecute `sql/01-esquema.sql`. Use la cadena del panel para configurar `ConnectionStrings__Inventario` en su terminal, con cifrado TLS, y repita el paso de creación del usuario de evaluación. [Guía de SQL Server de Somee](https://somee.com/Faq)
3. Genere los archivos del sitio. Si el panel ofrece .NET 10, use la primera publicación. Si falta ese runtime y el sitio tiene ANCM v2 con un proceso IIS de 64 bits, genere la variante autocontenida.

   ```powershell
   dotnet publish -c Release -o artifacts/somee
   # Variante autocontenida
   dotnet publish -c Release -r win-x64 --self-contained true -o artifacts/somee-win-x64
   ```

4. Suba el **contenido** de la publicación a la raíz del sitio mediante File Manager o FTP. En la copia de `web.config` del servidor, cambie el cierre `/>` del elemento `aspNetCore` por `>`, inserte el siguiente bloque y ciérrelo con `</aspNetCore>`. Conserve los atributos generados por `dotnet publish`. En el atributo `value`, escriba las entidades XML `&amp;` para el carácter `&`, `&quot;` para las comillas dobles y `&lt;` para el signo menor que. [Guía de carga de Somee](https://www.somee.com/Doka/Help/Article/69/Uploading_Your_Website_Content)

   ```xml
   <environmentVariables>
     <environmentVariable name="ConnectionStrings__Inventario" value="CADENA_SQL_DE_LA_CUENTA" />
   </environmentVariables>
   ```

   Mantenga la cadena SQL en la configuración del sitio y comparta con el evaluador únicamente el usuario de prueba de la API y su contraseña, por separado del código fuente.
5. Abra `https://SUBDOMINIO.somee.com/swagger` y ejecute la verificación desde PowerShell 7. El script crea un libro de prueba, comprueba las consultas y la actualización, lo elimina y verifica su ausencia.

   ```powershell
   ./scripts/Verificar-Api.ps1 -BaseUrl https://SUBDOMINIO.somee.com -Correo evaluacion@example.test
   ```
