using Microsoft.AspNetCore.Mvc;
using NetAI.SandboxOrchestration.Models;
using NetAI.SandboxOrchestration.Services;

namespace NetAI.SandboxOrchestration.Controllers;

[ApiController]
public class SandboxOrchestrationController : ControllerBase
{
    private readonly SandboxLifecycleService _sandboxLifecycleService;

    public SandboxOrchestrationController(SandboxLifecycleService sandboxLifecycleService)
    {
        _sandboxLifecycleService = sandboxLifecycleService;
    }

    [HttpPost("/orchestration/sandboxes/start")]
    public async Task<ActionResult<SandboxStartResponse>> StartSandboxOrchestration(CancellationToken cancellationToken)
        => await StartSandboxInternalAsync(cancellationToken);

    [HttpGet("/orchestration/sandboxes/health")]
    public async Task<ActionResult<SandboxHealthResponse>> GetSandboxOrchestrationHealth(CancellationToken cancellationToken)
        => await GetHealthInternalAsync(cancellationToken);

    [HttpPost("/orchestration/sandboxes/{sandboxId}/resume")]
    public async Task<ActionResult<SandboxLifecycleActionResponse>> ResumeSandbox(string sandboxId, CancellationToken cancellationToken)
        => await ExecuteLifecycleActionAsync("resume", sandboxId, cancellationToken);

    [HttpPost("/orchestration/sandboxes/{sandboxId}/pause")]
    public async Task<ActionResult<SandboxLifecycleActionResponse>> PauseSandbox(string sandboxId, CancellationToken cancellationToken)
        => await ExecuteLifecycleActionAsync("pause", sandboxId, cancellationToken);

    [HttpPost("/orchestration/sandboxes/{sandboxId}/stop")]
    public async Task<ActionResult<SandboxLifecycleActionResponse>> StopSandbox(string sandboxId, CancellationToken cancellationToken)
        => await ExecuteLifecycleActionAsync("stop", sandboxId, cancellationToken);

    [HttpPost("/v1/sandboxes/start")]
    public Task<ActionResult<SandboxStartResponse>> StartSandboxV1(CancellationToken cancellationToken)
        => StartSandboxInternalAsync(cancellationToken);

    [HttpGet("/v1/sandboxes/health")]
    public Task<ActionResult<SandboxHealthResponse>> GetSandboxV1Health(CancellationToken cancellationToken)
        => GetHealthInternalAsync(cancellationToken);

    [HttpPost("/sandbox/start")]
    [HttpPost("/api/sandbox/start")]
    [HttpPost("/api/sandboxes/start")]
    [HttpPost("/sandboxes/start")]
    [HttpPost("/api/orchestration/sandboxes/start")]
    [HttpPost("/orchestration/sandbox/start")]
    [HttpPost("/api/sandboxorchestration/sandboxes/start")]
    public Task<ActionResult<SandboxStartResponse>> StartSandboxAlias(CancellationToken cancellationToken)
        => StartSandboxInternalAsync(cancellationToken);

    [HttpGet("/sandbox/health")]
    [HttpGet("/api/sandbox/health")]
    [HttpGet("/api/sandboxes/health")]
    [HttpGet("/sandboxes/health")]
    [HttpGet("/api/orchestration/sandboxes/health")]
    [HttpGet("/orchestration/sandbox/health")]
    [HttpGet("/api/sandboxorchestration/sandboxes/health")]
    public Task<ActionResult<SandboxHealthResponse>> GetSandboxAliasHealth(CancellationToken cancellationToken)
        => GetHealthInternalAsync(cancellationToken);

    [HttpPost("/start")]
    public async Task<ActionResult<SandboxStartResponse>> StartRootSandbox(CancellationToken cancellationToken)
        => await StartSandboxInternalAsync(cancellationToken);

    private async Task<ActionResult<SandboxStartResponse>> StartSandboxInternalAsync(CancellationToken cancellationToken)
    {
        var startResponse = await _sandboxLifecycleService.StartAsync(cancellationToken);
        if (startResponse.IsSuccess)
        {
            if (string.IsNullOrWhiteSpace(startResponse.Message))
            {
                startResponse.Message = $"Sandbox start requested via {HttpContext.Request.Path}";
            }
            else
            {
                startResponse.Message = $"{startResponse.Message} (requested via {HttpContext.Request.Path})";
            }
        }

        return Ok(startResponse);
    }

    private async Task<ActionResult<SandboxHealthResponse>> GetHealthInternalAsync(CancellationToken cancellationToken)
    {
        var healthResponse = await _sandboxLifecycleService.GetHealthAsync(cancellationToken);
        return Ok(healthResponse);
    }

    private async Task<ActionResult<SandboxLifecycleActionResponse>> ExecuteLifecycleActionAsync(string action, string sandboxId, CancellationToken cancellationToken)
    {
        SandboxLifecycleActionResponse result = action switch
        {
            "resume" => await _sandboxLifecycleService.ResumeAsync(sandboxId, cancellationToken),
            "pause" => await _sandboxLifecycleService.PauseAsync(sandboxId, cancellationToken),
            "stop" => await _sandboxLifecycleService.StopAsync(sandboxId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported lifecycle action.")
        };

        return Ok(result);
    }
}
