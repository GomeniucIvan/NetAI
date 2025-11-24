using NetAI.Api.Models.Configuration;

namespace NetAI.Api.Services.Configuration;

public interface IOptionsMetadataService
{
    IReadOnlyList<string> GetModels();

    IReadOnlyList<string> GetAgents();

    IReadOnlyList<string> GetSecurityAnalyzers();

    GetConfigResponseDto GetConfig();
}
