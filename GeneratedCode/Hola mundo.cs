// Generado el 2026-02-27 15:57:48
// Basado en User Story: Hola mundo
// Código generado por IA (Gemini)

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Globalization;

namespace HolaMundoApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configura los servicios de la aplicación
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Calculadora API", Version = "v1" });
            });

            var app = builder.Build();

            // Configura el pipeline de solicitudes HTTP
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Calculadora API v1");
                    c.RoutePrefix = "swagger"; // La interfaz de usuario de Swagger estará en /swagger
                });
            }

            // Endpoint para la página principal con la interfaz de usuario de la calculadora
            app.MapGet("/", () =>
            {
                var htmlContent = """
                <!DOCTYPE html>
                <html lang="es">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Calculadora Simple</title>
                    <style>
                        body { 
                            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                            display: flex; 
                            justify-content: center; 
                            align-items: center; 
                            min-height: 100vh; 
                            background-color: #e0f2f7; 
                            margin: 0; 
                            color: #333;
                        }
                        .calculator-container { 
                            background-color: #ffffff; 
                            padding: 30px; 
                            border-radius: 12px; 
                            box-shadow: 0 6px 15px rgba(0,0,0,0.15); 
                            text-align: center; 
                            width: 320px;
                        }
                        h1 { 
                            color: #007bff; 
                            margin-bottom: 20px; 
                            font-size: 2em; 
                        }
                        input[type="number"] { 
                            width: 120px; 
                            padding: 10px; 
                            margin: 8px; 
                            border: 1px solid #cceeff; 
                            border-radius: 6px; 
                            font-size: 1.1em;
                            text-align: right;
                        }
                        .button-group {
                            margin-top: 20px;
                        }
                        button { 
                            padding: 12px 20px; 
                            margin: 6px; 
                            border: none; 
                            border-radius: 6px; 
                            cursor: pointer; 
                            background-color: #007bff; 
                            color: white; 
                            font-size: 1.2em; 
                            transition: background-color 0.2s ease, transform 0.1s ease;
                        }
                        button:hover { 
                            background-color: #0056b3; 
                            transform: translateY(-2px);
                        }
                        button:active {
                            transform: translateY(0);
                        }
                        #result { 
                            margin-top: 25px; 
                            font-size: 2.2em; 
                            font-weight: bold; 
                            color: #28a745; 
                            background-color: #e9fbec;
                            padding: 15px;
                            border-radius: 8px;
                            border: 1px solid #d4edda;
                            min-height: 1.2em; /* Ensure some height even if empty */
                            display: flex;
                            justify-content: center;
                            align-items: center;
                        }
                        .error { 
                            color: #dc3545; 
                            font-size: 1.1em;
                        }
                    </style>
                </head>
                <body>
                    <div class="calculator-container">
                        <h1>Calculadora Simple</h1>
                        <input type="number" id="num1" value="0" placeholder="Número 1">
                        <input type="number" id="num2" value="0" placeholder="Número 2">
                        <div class="button-group">
                            <button onclick="calculate('add')">+</button>
                            <button onclick="calculate('subtract')">-</button>
                            <button onclick="calculate('multiply')">*</button>
                            <button onclick="calculate('divide')">/</button>
                        </div>
                        <div id="result">Resultado: 0</div>
                    </div>

                    <script>
                        async function calculate(operation) {
                            const num1 = parseFloat(document.getElementById('num1').value);
                            const num2 = parseFloat(document.getElementById('num2').value);
                            const resultDiv = document.getElementById('result');

                            if (isNaN(num1) || isNaN(num2)) {
                                resultDiv.innerHTML = '<span class="error">Por favor, introduce números válidos.</span>';
                                resultDiv.style.color = '#dc3545'; // Set error color
                                resultDiv.style.backgroundColor = '#f8d7da'; // Set error background
                                return;
                            }

                            try {
                                const response = await fetch(`/api/calculator/${operation}?num1=${num1}&num2=${num2}`);
                                const data = await response.json();

                                if (response.ok) {
                                    resultDiv.textContent = 'Resultado: ' + data.result;
                                    resultDiv.style.color = '#28a745'; // Set success color
                                    resultDiv.style.backgroundColor = '#e9fbec'; // Set success background
                                } else {
                                    resultDiv.innerHTML = '<span class="error">' + (data.message || 'Error desconocido') + '</span>';
                                    resultDiv.style.color = '#dc3545'; // Set error color
                                    resultDiv.style.backgroundColor = '#f8d7da'; // Set error background
                                }
                            } catch (error) {
                                resultDiv.innerHTML = '<span class="error">Error de conexión: ' + error.message + '</span>';
                                resultDiv.style.color = '#dc3545'; // Set error color
                                resultDiv.style.backgroundColor = '#f8d7da'; // Set error background
                            }
                        }
                    </script>
                </body>
                </html>
                """;
                return Results.Content(htmlContent, "text/html");
            });

            // API Endpoints para las operaciones de la calculadora
            app.MapGet("/api/calculator/add", (double num1, double num2) =>
            {
                return Results.Ok(new { result = num1 + num2 });
            })
            .WithName("Add")
            .WithOpenApi();

            app.MapGet("/api/calculator/subtract", (double num1, double num2) =>
            {
                return Results.Ok(new { result = num1 - num2 });
            })
            .WithName("Subtract")
            .WithOpenApi();

            app.MapGet("/api/calculator/multiply", (double num1, double num2) =>
            {
                return Results.Ok(new { result = num1 * num2 });
            })
            .WithName("Multiply")
            .WithOpenApi();

            app.MapGet("/api/calculator/divide", (double num1, double num2) =>
            {
                if (num2 == 0)
                {
                    return Results.BadRequest(new { message = "No se puede dividir por cero." });
                }
                return Results.Ok(new { result = num1 / num2 });
            })
            .WithName("Divide")
            .WithOpenApi();

            app.Run();
        }
    }
}