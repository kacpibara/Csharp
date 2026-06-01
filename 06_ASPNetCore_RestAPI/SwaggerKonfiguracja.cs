using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Runtime.Serialization;

namespace _06_ASPNetCore_RestAPI;

// ============================================================
// SWAGGER GEN KONFIGURACJA
// ============================================================

public static class SwaggerKonfiguracja
{
    public static IServiceCollection DodajSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // OpenApiInfo — metadane dokumentu
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Moje API",
                Version = "v1",
                Description = "Demonstracja wszystkich funkcji Swagger/OpenAPI w ASP.NET Core 8",
                Contact = new OpenApiContact
                {
                    Name = "Kacper Barancewicz",
                    Email = "kacperbarancewicz@gmail.com",
                    Url = new Uri("https://example.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                },
                TermsOfService = new Uri("https://example.com/terms")
            });

            // XML komentarze (GenerateDocumentationFile=true w .csproj)
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);

            // JWT Bearer security definition
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Wprowadź token JWT: Bearer {token}"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Custom filters
            options.OperationFilter<GlobalNaglowkiFilter>();
            options.DocumentFilter<HealthCheckDocumentFilter>();
            options.SchemaFilter<EnumSchemaFilter>();

            // Grupowanie operacji
            options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "default" });
        });

        return services;
    }

    public static IApplicationBuilder UzyjSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Moje API v1");
            options.RoutePrefix = "swagger";

            // Customizacja UI
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            options.DefaultModelsExpandDepth(-1); // Ukryj modele domyślnie
        });

        return app;
    }
}

// ============================================================
// CUSTOM OPERATION FILTER — GLOBALNE NAGŁÓWKI
// ============================================================

/// <summary>
/// Dodaje X-Correlation-Id do każdej operacji w dokumentacji
/// </summary>
public class GlobalNaglowkiFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-Id",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
            Description = "Identyfikator korelacji dla śledzenia requestów"
        });
    }
}

// ============================================================
// CUSTOM DOCUMENT FILTER — DODAJE HEALTH CHECK DO DOKUMENTU
// ============================================================

public class HealthCheckDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Paths.TryAdd("/health", new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = [new OpenApiTag { Name = "Health" }],
                    Summary = "Sprawdź stan aplikacji",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Aplikacja działa" },
                        ["503"] = new OpenApiResponse { Description = "Aplikacja niedostępna" }
                    }
                }
            }
        });
    }
}

// ============================================================
// CUSTOM SCHEMA FILTER — ENUM JAKO STRINGS Z OPISEM
// ============================================================

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        schema.Enum.Clear();
        var enumValues = Enum.GetValues(context.Type);

        foreach (var value in enumValues)
        {
            var memberInfo = context.Type.GetMember(value.ToString()!).FirstOrDefault();
            var enumMember = memberInfo?.GetCustomAttribute<EnumMemberAttribute>();
            var displayName = enumMember?.Value ?? value.ToString()!;
            schema.Enum.Add(new OpenApiString(displayName));
        }

        // Dodaj opis z wartościami
        schema.Description = $"Możliwe wartości: {string.Join(", ", enumValues.Cast<object>())}";
    }
}

// ============================================================
// PRODUCESRESPONSETYPE + XML COMMENTS DEMO
// ============================================================

/// <summary>
/// Przykładowy kontroler z pełną dokumentacją XML
/// </summary>
public class SwaggerDemoKontroler
{
    /// <summary>
    /// Pobiera produkt po identyfikatorze
    /// </summary>
    /// <param name="id">Identyfikator produktu (liczba całkowita > 0)</param>
    /// <returns>Dane produktu</returns>
    /// <response code="200">Produkt znaleziony i zwrócony</response>
    /// <response code="404">Produkt o podanym ID nie istnieje</response>
    /// <response code="400">Nieprawidłowe ID (musi być > 0)</response>
    // [ProducesResponseType(typeof(ProduktDto), 200)]
    // [ProducesResponseType(typeof(ProblemDetails), 404)]
    // [ProducesResponseType(typeof(ProblemDetails), 400)]
    public void PobierzProdukt(int id) { }
}

// ============================================================
// MINIMAL API + SWAGGER
// ============================================================

public static class MinimalApiSwaggerExtensions
{
    public static void MapMinimalApiEndpointy(this WebApplication app)
    {
        // .WithName() — nadaje nazwę endpointowi (używane w LinkGenerator)
        // .WithSummary() — krótki opis w Swagger UI
        // .WithTags() — grupowanie w Swagger UI
        // .Produces<T>() — dokumentuje typ odpowiedzi
        // .ProducesProblem() — dokumentuje kody błędów

        var group = app.MapGroup("/api/minimal")
            .WithTags("MinimalAPI");

        group.MapGet("/produkty", () =>
        {
            var produkty = new[] {
                new { Id = 1, Nazwa = "Produkt A", Cena = 10.99m },
                new { Id = 2, Nazwa = "Produkt B", Cena = 24.99m }
            };
            return Results.Ok(produkty);
        })
        .WithName("PobierzMinimalProdukty")
        .WithSummary("Pobiera listę produktów (Minimal API)")
        .Produces<object[]>(200);

        group.MapGet("/produkty/{id:int}", (int id) =>
        {
            if (id <= 0) return Results.Problem("ID musi być > 0", statusCode: 400);
            if (id > 100) return Results.NotFound();
            return Results.Ok(new { Id = id, Nazwa = $"Produkt {id}", Cena = 9.99m });
        })
        .WithName("PobierzMinimalProdukt")
        .WithSummary("Pobiera produkt po ID (Minimal API)")
        .Produces<object>(200)
        .ProducesProblem(400)
        .ProducesProblem(404);

        // EndpointFilter
        group.MapPost("/produkty", (object nowyProdukt) =>
            Results.Created($"/api/minimal/produkty/1", nowyProdukt))
        .WithName("UtworzMinimalProdukt")
        .WithSummary("Tworzy nowy produkt (Minimal API)")
        .AddEndpointFilter(async (context, next) =>
        {
            // EndpointFilter — jak ActionFilter, ale dla Minimal API
            // Wykonuje się przed i po handlerze
            var wynik = await next(context);
            return wynik;
        });
    }
}
