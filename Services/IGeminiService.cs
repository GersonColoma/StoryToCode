namespace StoryToCode.Services;

public interface IGeminiService
{
    Task<string?> GenerateCodeFromStoryAsync(string title, string description, string acceptanceCriteria);
}
