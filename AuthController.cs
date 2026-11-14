using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InventarioLibros;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(InventarioDb db, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginSolicitud solicitud, CancellationToken cancellationToken)
    {
        var correo = solicitud.Correo.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(
            usuario => usuario.Correo == correo, cancellationToken);
        if (usuario is null ||
            hasher.VerifyHashedPassword(usuario, usuario.HashContrasena, solicitud.Contrasena)
                == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString())],
            BearerTokenDefaults.AuthenticationScheme);
        Response.Headers.CacheControl = "no-store";
        return SignIn(new ClaimsPrincipal(identidad), BearerTokenDefaults.AuthenticationScheme);
    }
}
