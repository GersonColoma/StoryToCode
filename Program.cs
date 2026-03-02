using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryToCode.Models;
using StoryToCode.Services;
using System.Text.RegularExpressions;

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
        // Crear un nombre de carpeta seguro a partir del título
        string safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        string projectDir = Path.Combine(outputDir, safeTitle);
        Directory.CreateDirectory(projectDir);

        logger.LogInformation("Procesando respuesta de Gemini para crear múltiples archivos en {ProjectDir}", projectDir);

        // Expresión regular para encontrar los bloques: ### archivo: ruta/al/archivo luego el contenido entre ```
        var regex = new Regex(@"### archivo: (.*?)\r?\n```(?:\w*)\r?\n(.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = regex.Matches(code);

        if (matches.Count == 0)
        {
            // Si no encuentra bloques, guarda todo en un solo archivo como antes
            logger.LogWarning("No se encontraron bloques de archivos en la respuesta. Guardando como un solo archivo.");
            string fallbackPath = Path.Combine(projectDir, $"{safeTitle}.txt");
            await File.WriteAllTextAsync(fallbackPath, code);
            logger.LogInformation("Archivo único guardado en: {FilePath}", fallbackPath);
            return;
        }

        foreach (Match match in matches)
        {
            string relativePath = match.Groups[1].Value.Trim();
            string content = match.Groups[2].Value.Trim();

            // Asegurar que la ruta relativa no tenga caracteres inválidos
            string fullPath = Path.Combine(projectDir, relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content);
            logger.LogInformation("Archivo creado: {FilePath}", fullPath);
        }

        logger.LogInformation("Proyecto generado exitosamente en {ProjectDir}", projectDir);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al guardar los archivos del proyecto");
        throw;
    }
}
}
