using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventarioLibros;

internal sealed class SeguridadOpenApi : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto,
        CancellationToken cancellationToken)
    {
        documento.Info.Title = "Inventario de libros";
        documento.Components ??= new();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Pegue el accessToken de POST /api/auth/login."
        };
        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operacion, OpenApiOperationTransformerContext contexto,
        CancellationToken cancellationToken)
    {
        if (contexto.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operacion.Security = [new() { [new OpenApiSecuritySchemeReference("Bearer", contexto.Document)] = [] }];
            operacion.Responses ??= new();
            operacion.Responses.TryAdd("401", new OpenApiResponse { Description = "Se requiere un token válido." });
        }

        return Task.CompletedTask;
    }
}
