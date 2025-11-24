using System.Threading;
using System.Threading.Tasks;
using NetAI.SandboxOrchestration.Models;

namespace NetAI.SandboxOrchestration.Services;

public interface IOpenHandsClient
{
    Task<OpenHandsConversationResult> CreateConversationAsync(CancellationToken cancellationToken = default);
    Task<OpenHandsConversationResult> StartConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<OpenHandsConversationResult> CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<OpenHandsConversationResult> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<ServiceHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
}
