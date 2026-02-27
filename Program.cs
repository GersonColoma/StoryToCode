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
