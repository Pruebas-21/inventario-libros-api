using System.Threading.RateLimiting;
using InventarioLibros;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var crearUsuario = args is ["crear-usuario"];
var builder = WebApplication.CreateBuilder(crearUsuario ? [] : args);

var conexion = builder.Configuration.GetConnectionString("Inventario")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Inventario.");

builder.Services.AddDbContext<InventarioDb>(options => options.UseSqlServer(conexion));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(options => options.BearerTokenExpiration = TimeSpan.FromHours(1));
builder.Services.AddAuthorization();
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<SeguridadOpenApi>();
    options.AddOperationTransformer<SeguridadOpenApi>();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var app = builder.Build();
if (crearUsuario)
{
    return await ProvisionUsuario.EjecutarAsync(app.Services);
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
var rutaPanel = Path.Combine(app.Environment.WebRootPath, "index.html");
app.MapGet("/", async (HttpContext contexto) =>
{
    var panel = await File.ReadAllTextAsync(rutaPanel, contexto.RequestAborted);
    contexto.Response.ContentType = "text/html; charset=utf-8";
    contexto.Response.Headers.CacheControl = "no-cache";
    await contexto.Response.WriteAsync(panel, contexto.RequestAborted);
}).ExcludeFromDescription();
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Inventario de libros");
    options.DocumentTitle = "Inventario de libros";
});
app.MapControllers();

await app.RunAsync();
return 0;
