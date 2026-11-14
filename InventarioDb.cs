using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace InventarioLibros;

public sealed class InventarioDb(DbContextOptions<InventarioDb> options) : DbContext(options)
{
    public DbSet<Libro> Libros => Set<Libro>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Libro>().ToTable("Libros", "dbo");
        modelBuilder.Entity<Usuario>().ToTable("Usuarios", "dbo");
        modelBuilder.Entity<Usuario>().HasIndex(usuario => usuario.Correo).IsUnique();
    }
}

public sealed class Libro
{
    public int Id { get; set; }
    [MaxLength(160)] public required string Titulo { get; set; }
    [MaxLength(120)] public required string Autor { get; set; }
    public int AnioPublicacion { get; set; }
    public int Existencias { get; set; }

    public void Actualizar(LibroSolicitud solicitud)
    {
        Titulo = solicitud.Titulo.Trim();
        Autor = solicitud.Autor.Trim();
        AnioPublicacion = solicitud.AnioPublicacion!.Value;
        Existencias = solicitud.Existencias!.Value;
    }
}

public sealed class Usuario
{
    public int Id { get; set; }
    [MaxLength(254)] public required string Correo { get; set; }
    [MaxLength(512)] public required string HashContrasena { get; set; }
}

public sealed record LibroSolicitud(
    [Required, StringLength(160)] string Titulo,
    [Required, StringLength(120)] string Autor,
    [Required, Range(1, 9999)] int? AnioPublicacion,
    [Required, Range(0, int.MaxValue)] int? Existencias);

public sealed record LoginSolicitud(
    [Required, EmailAddress, StringLength(254)] string Correo,
    [Required, StringLength(128)] string Contrasena);
