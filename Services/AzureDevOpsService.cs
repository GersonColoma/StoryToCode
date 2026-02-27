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
