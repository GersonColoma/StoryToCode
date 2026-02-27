using StoryToCode.Models;

namespace StoryToCode.Services;

public interface IAzureDevOpsService
{
    Task<UserStory?> GetUserStoryAsync(int storyId);
}
