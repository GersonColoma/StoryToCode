# Crear carpetas
mkdir -p Models Services

# Crear Models/AppConfig.cs
cat > Models/AppConfig.cs << 'EOF'
using System;

namespace StoryToCode.Models;

public class AppConfig
{
    public AzureDevOpsConfig AzureDevOps { get; set; } = new();
    public GeminiConfig Gemini { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
}

public class AzureDevOpsConfig
{
    public string OrganizationUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
}

public class GeminiConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/";
}

public class OutputConfig
{
    public string Directory { get; set; } = "./GeneratedCode";
}
EOF

# Crear Models/UserStory.cs
cat > Models/UserStory.cs << 'EOF'
namespace StoryToCode.Models;

public class UserStory
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
}
EOF

# Crear Services/IAzureDevOpsService.cs
cat > Services/IAzureDevOpsService.cs << 'EOF'
using StoryToCode.Models;

namespace StoryToCode.Services;

public interface IAzureDevOpsService
{
    Task<UserStory?> GetUserStoryAsync(int storyId);
}
EOF

# Crear Services/AzureDevOpsService.cs
cat > Services/AzureDevOpsService.cs << 'EOF'
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryToCode.Models;

namespace StoryToCode.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly AzureDevOpsConfig _config;
    private readonly WorkItemTrackingHttpClient _witClient;

    public AzureDevOpsService(ILogger<AzureDevOpsService> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        _config = config.Value.AzureDevOps;

        var credentials = new VssBasicCredential(string.Empty, _config.PersonalAccessToken);
        var connection = new VssConnection(new Uri(_config.OrganizationUrl), credentials);
        _witClient = connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<UserStory?> GetUserStoryAsync(int storyId)
    {
        try
        {
            _logger.LogInformation("Obteniendo User Story {StoryId} desde Azure DevOps...", storyId);

            var fields = new[] { "System.Title", "System.Description", "Microsoft.VSTS.Common.AcceptanceCriteria" };
            var workItem = await _witClient.GetWorkItemAsync(storyId, fields);

            if (workItem == null)
            {
                _logger.LogWarning("No se encontró la User Story con ID {StoryId}", storyId);
                return null;
            }

            var story = new UserStory
            {
                Id = workItem.Id ?? 0,
                Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty,
                Description = workItem.Fields["System.Description"]?.ToString() ?? string.Empty,
                AcceptanceCriteria = workItem.Fields.ContainsKey("Microsoft.VSTS.Common.AcceptanceCriteria") 
                    ? workItem.Fields["Microsoft.VSTS.Common.AcceptanceCriteria"]?.ToString() ?? string.Empty 
                    : string.Empty
            };

            _logger.LogInformation("User Story obtenida: {Title}", story.Title);
            return story;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la User Story {StoryId}", storyId);
            throw;
        }
    }
}
EOF

# Crear Services/IGeminiService.cs
cat > Services/IGeminiService.cs << 'EOF'
namespace StoryToCode.Services;

public interface IGeminiService
{
    Task<string?> GenerateCodeFromStoryAsync(string title, string description, string acceptanceCriteria);
}
EOF

# Crear Services/GeminiService.cs
cat > Services/GeminiService.cs << 'EOF'
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StoryToCode.Models;

namespace StoryToCode.Services;

public class GeminiService : IGeminiService
{
    private readonly ILogger<GeminiService> _logger;
    private readonly GeminiConfig _config;
    private readonly HttpClient _httpClient;

    public GeminiService(ILogger<GeminiService> logger, IOptions<AppConfig> config, HttpClient httpClient)
    {
        _logger = logger;
        _config = config.Value.Gemini;
        _httpClient = httpClient;
    }

    public async Task<string?> GenerateCodeFromStoryAsync(string title, string description, string acceptanceCriteria)
    {
        try
        {
            _logger.LogInformation("Enviando solicitud a Gemini API...");

            var prompt = $@"
Eres un asistente experto en generar código C# de alta calidad.

Basándote en la siguiente User Story de Azure DevOps, genera las clases y métodos necesarios en C# para implementar la funcionalidad descrita.

Título: {title}

Descripción:
{description}

Criterios de Aceptación:
{acceptanceCriteria}

Requisitos:
- Genera código C# moderno ( .NET 8+).
- Incluye namespaces apropiados.
- Si es una aplicación de consola, incluye el método Main.
- Si necesita persistencia, usa Entity Framework Core o simplemente clases en memoria.
- El código debe ser autocontenido y compilable.
- No incluyas explicaciones extensas, solo el código.
- El nombre de la clase principal debe derivarse del título.

Código generado:
";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(requestBody, jsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_config.ApiUrl}{_config.Model}:generateContent?key={_config.ApiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error en Gemini API: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(responseString);

            var generatedCode = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(generatedCode))
            {
                _logger.LogWarning("Gemini no generó código");
                return null;
            }

            generatedCode = CleanCodeFromMarkdown(generatedCode);
            _logger.LogInformation("Código generado exitosamente");
            return generatedCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar código con Gemini");
            throw;
        }
    }

    private string CleanCodeFromMarkdown(string code)
    {
        var lines = code.Split('\n');
        if (lines.Length > 0 && lines[0].Trim().StartsWith("```"))
        {
            code = string.Join("\n", lines.Skip(1).TakeWhile(l => !l.Trim().StartsWith("```")));
        }
        return code.Trim();
    }

    private class GeminiResponse
    {
        public Candidate[]? Candidates { get; set; }
    }

    private class Candidate
    {
        public Content? Content { get; set; }
    }

    private class Content
    {
        public Part[]? Parts { get; set; }
    }

    private class Part
    {
        public string? Text { get; set; }
    }
}
EOF

# Crear Program.cs (reemplaza el existente)
cat > Program.cs << 'EOF'
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryToCode.Models;
using StoryToCode.Services;

namespace StoryToCode;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = CreateHostBuilder(args).Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Iniciando StoryToCode Generator");

            Console.Write("Ingresa el ID de la User Story de Azure DevOps: ");
            if (!int.TryParse(Console.ReadLine(), out int storyId))
            {
                logger.LogError("ID inválido");
                return;
            }

            var azureService = host.Services.GetRequiredService<IAzureDevOpsService>();
            var geminiService = host.Services.GetRequiredService<IGeminiService>();
            var config = host.Services.GetRequiredService<IOptions<AppConfig>>().Value;

            logger.LogInformation("Paso 1: Leyendo User Story {StoryId}...", storyId);
            var story = await azureService.GetUserStoryAsync(storyId);
            
            if (story == null)
            {
                logger.LogError("No se pudo obtener la User Story");
                return;
            }

            logger.LogInformation("Paso 2: Interpretando User Story");
            Console.WriteLine($"\nTítulo: {story.Title}");
            Console.WriteLine($"Descripción: {story.Description[..Math.Min(100, story.Description.Length)]}...");
            Console.WriteLine($"Criterios: {story.AcceptanceCriteria[..Math.Min(100, story.AcceptanceCriteria.Length)]}...\n");

            logger.LogInformation("Paso 3: Generando código con IA...");
            var generatedCode = await geminiService.GenerateCodeFromStoryAsync(
                story.Title, 
                story.Description, 
                story.AcceptanceCriteria);

            if (string.IsNullOrWhiteSpace(generatedCode))
            {
                logger.LogError("No se pudo generar código");
                return;
            }

            logger.LogInformation("Paso 4: Guardando código generado");
            await SaveCodeToFile(story.Title, generatedCode, config.Output.Directory, logger);

            logger.LogInformation("Proceso completado exitosamente");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fatal en la aplicación");
        }
        finally
        {
            Console.WriteLine("\nPresiona cualquier tecla para salir...");
            Console.ReadKey();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AppConfig>(context.Configuration);
                services.AddHttpClient<IGeminiService, GeminiService>();
                services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.AddConfiguration(context.Configuration.GetSection("Logging"));
                });
            });

    static async Task SaveCodeToFile(string title, string code, string outputDir, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeTitle}.cs";
            var filePath = Path.Combine(outputDir, fileName);

            var header = $@"// Generado el {DateTime.Now:yyyy-MM-dd HH:mm:ss}
// Basado en User Story: {title}
// Código generado por IA (Gemini)

";

            await File.WriteAllTextAsync(filePath, header + code);
            logger.LogInformation("Archivo guardado en: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al guardar el archivo");
            throw;
        }
    }
}
EOF

# Crear appsettings.json
cat > appsettings.json << 'EOF'
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/tu-organizacion",
    "ProjectName": "NombreDelProyecto",
    "PersonalAccessToken": "tu-pat-aqui"
  },
  "Gemini": {
    "ApiKey": "tu-api-key-de-gemini",
    "Model": "gemini-1.5-flash",
    "ApiUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
  },
  "Output": {
    "Directory": "./GeneratedCode"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
EOF

echo "✅ Todos los archivos han sido creados."
echo "⚠️  No olvides editar 'appsettings.json' con tus credenciales reales."
