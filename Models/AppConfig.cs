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
