using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventarioLibros;

internal static class ProvisionUsuario
{
    public static async Task<int> EjecutarAsync(IServiceProvider servicios)
    {
        var correo = Environment.GetEnvironmentVariable("INVENTARIO_USUARIO_CORREO")?.Trim().ToLowerInvariant();
        var contrasena = Environment.GetEnvironmentVariable("INVENTARIO_USUARIO_CONTRASENA");
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > 254 || !new EmailAddressAttribute().IsValid(correo) ||
            contrasena is null || contrasena.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(contrasena))
        {
            Console.Error.WriteLine("Configure un correo válido y una contraseña de 12 a 128 caracteres en las variables de aprovisionamiento.");
            return 1;
        }

        await using var scope = servicios.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDb>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();

        if (await db.Usuarios.AnyAsync(usuario => usuario.Correo == correo))
        {
            Console.Error.WriteLine("Ya existe un usuario con ese correo.");
            return 1;
        }

        var usuario = new Usuario { Correo = correo, HashContrasena = "" };
        usuario.HashContrasena = hasher.HashPassword(usuario, contrasena);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        Console.WriteLine("Usuario creado.");
        return 0;
    }
}
