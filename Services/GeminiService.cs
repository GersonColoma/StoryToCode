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
