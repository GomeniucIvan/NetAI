using System;
using System.Threading;
using System.Threading.Tasks;
using NetAI.SandboxOrchestration.Models;

namespace NetAI.SandboxOrchestration.Services;

public class SandboxLifecycleService
{
    private const string ServiceName = "openhands";

    private readonly IOpenHandsClient _openHandsClient;
    private readonly ILogger<SandboxLifecycleService> _logger;

    public SandboxLifecycleService(IOpenHandsClient openHandsClient, ILogger<SandboxLifecycleService> logger)
    {
        _openHandsClient = openHandsClient;
        _logger = logger;
    }

    public async Task<SandboxStartResponse> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            OpenHandsConversationResult result = await _openHandsClient
                .CreateConversationAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "OpenHands sandbox start attempt reported Succeeded={Succeeded}, Status={Status}, RuntimeStatus={RuntimeStatus}, Message={Message}, SandboxId={SandboxId}, RuntimeUrl={RuntimeUrl}.",
                result.Succeeded,
                result.Status,
                result.RuntimeStatus,
                result.Message,
                result.SandboxId,
                result.RuntimeUrl);

            if (!result.Succeeded)
            {
                return CreateStartFailure(
                    result.Message ?? "OpenHands reported an error while starting the conversation.",
                    string.IsNullOrWhiteSpace(result.Status) ? $"{ServiceName}:error" : result.Status);
            }

            return MapStartSuccess(result);
        }
        catch (SandboxServiceException ex)
        {
            _logger.LogError(ex, "Failed to start OpenHands sandbox.");
            return CreateStartFailure(ex.Message, ex.DerivedStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while starting an OpenHands sandbox.");
            return CreateStartFailure(ex.Message, $"{ServiceName}:error");
        }
    }

    public Task<SandboxLifecycleActionResponse> ResumeAsync(string sandboxId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("resume", sandboxId, () => _openHandsClient.StartConversationAsync(sandboxId, cancellationToken));

    public Task<SandboxLifecycleActionResponse> PauseAsync(string sandboxId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("pause", sandboxId, () => _openHandsClient.CloseConversationAsync(sandboxId, cancellationToken));

    public Task<SandboxLifecycleActionResponse> StopAsync(string sandboxId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("stop", sandboxId, () => _openHandsClient.CloseConversationAsync(sandboxId, cancellationToken));

    public async Task<SandboxHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        ServiceHealthResponse health = await _openHandsClient.GetHealthAsync(cancellationToken).ConfigureAwait(false);

        string status = health.IsHealthy ? "ok" : "degraded";
        string details = string.IsNullOrWhiteSpace(health.Message) ? null : health.Message;

        return new SandboxHealthResponse
        {
            Status = status,
            RuntimeStatus = health.Status,
            BuildStatus = "n/a",
            CheckedAt = DateTimeOffset.UtcNow,
            Details = details
        };
    }

    private SandboxStartResponse MapStartSuccess(OpenHandsConversationResult result)
    {
        string status = !string.IsNullOrWhiteSpace(result.RuntimeStatus) ? result.RuntimeStatus! : result.Status;
        string message = string.IsNullOrWhiteSpace(result.Message)
            ? "OpenHands sandbox started successfully."
            : result.Message!;

        return new SandboxStartResponse
        {
            SandboxId = string.IsNullOrWhiteSpace(result.SandboxId) ? result.ConversationId : result.SandboxId,
            Status = string.IsNullOrWhiteSpace(status) ? "RUNNING" : status,
            RuntimeUrl = result.RuntimeUrl ?? string.Empty,
            SessionApiKey = result.SessionApiKey ?? string.Empty,
            WorkspacePath = string.Empty,
            SandboxSpecId = result.ConversationId,
            Message = message,
            IsSuccess = true
        };
    }

    private async Task<SandboxLifecycleActionResponse> ExecuteActionAsync(
        string action,
        string sandboxId,
        Func<Task<OpenHandsConversationResult>> operation)
    {
        try
        {
            OpenHandsConversationResult result = await operation().ConfigureAwait(false);
            string status = !string.IsNullOrWhiteSpace(result.Status)
                ? result.Status
                : result.Succeeded ? "ok" : $"{ServiceName}:error";

            string message = string.IsNullOrWhiteSpace(result.Message)
                ? $"OpenHands conversation {action} completed."
                : result.Message!;

            return new SandboxLifecycleActionResponse
            {
                SandboxId = string.IsNullOrWhiteSpace(result.SandboxId) ? sandboxId : result.SandboxId,
                Action = action,
                Status = status,
                Message = message,
                IsSuccess = result.Succeeded
            };
        }
        catch (SandboxServiceException ex)
        {
            _logger.LogWarning(ex, "OpenHands {Action} command failed for sandbox {SandboxId}.", action, sandboxId);
            return new SandboxLifecycleActionResponse
            {
                SandboxId = sandboxId,
                Action = action,
                Status = ex.DerivedStatus,
                Message = ex.Message,
                IsSuccess = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenHands {Action} command failed for sandbox {SandboxId}.", action, sandboxId);
            return new SandboxLifecycleActionResponse
            {
                SandboxId = sandboxId,
                Action = action,
                Status = $"{ServiceName}:error",
                Message = ex.Message,
                IsSuccess = false
            };
        }
    }

    private static SandboxStartResponse CreateStartFailure(string message, string status)
        => new()
        {
            SandboxId = string.Empty,
            Status = status,
            RuntimeUrl = string.Empty,
            SessionApiKey = string.Empty,
            WorkspacePath = string.Empty,
            SandboxSpecId = string.Empty,
            Message = message,
            IsSuccess = false
        };
}
