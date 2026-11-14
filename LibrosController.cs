using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioLibros;

[ApiController]
[Route("api/libros")]
public sealed class LibrosController(InventarioDb db) : ControllerBase
{
    [HttpGet]
    public async Task<List<Libro>> ConsultarTodos(CancellationToken cancellationToken)
        => await db.Libros.AsNoTracking().OrderBy(libro => libro.Id).ToListAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<Libro>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Libro>> Consultar(int id, CancellationToken cancellationToken)
    {
        var libro = await db.Libros.AsNoTracking().SingleOrDefaultAsync(
            libro => libro.Id == id, cancellationToken);
        return libro is null ? NotFound() : Ok(libro);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType<Libro>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Libro>> Crear(LibroSolicitud solicitud, CancellationToken cancellationToken)
    {
        var libro = new Libro
        {
            Titulo = solicitud.Titulo.Trim(),
            Autor = solicitud.Autor.Trim(),
            AnioPublicacion = solicitud.AnioPublicacion!.Value,
            Existencias = solicitud.Existencias!.Value
        };
        db.Libros.Add(libro);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Consultar), new { id = libro.Id }, libro);
    }

    [Authorize]
    [HttpPut("{id:int}")]
    [ProducesResponseType<Libro>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Libro>> Actualizar(int id, LibroSolicitud solicitud, CancellationToken cancellationToken)
    {
        var libro = await db.Libros.FindAsync([id], cancellationToken);
        if (libro is null)
        {
            return NotFound();
        }

        libro.Actualizar(solicitud);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }

        return Ok(libro);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        var eliminados = await db.Libros.Where(libro => libro.Id == id).ExecuteDeleteAsync(cancellationToken);
        return eliminados == 0 ? NotFound() : NoContent();
    }
}
