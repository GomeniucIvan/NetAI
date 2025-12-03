using System.Net.Http;

namespace NetAI.Http;

/// <summary>
/// Provides typed HTTP clients for interacting with runtime, server, and orchestration services.
/// This abstraction lives in the shared NetAI project so multiple solutions can obtain the same
/// service-specific clients without duplicating configuration details.
/// </summary>
public interface IRuntimeHttpClientProvider
{
    HttpClient GetRuntimeApiClient();

    HttpClient GetRuntimeServerClient();

    HttpClient GetSandboxOrchestrationClient();
}