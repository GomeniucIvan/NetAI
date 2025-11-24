using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Services.Conversations;

namespace NetAI.Api.Controllers;

//TODO ref

[ApiController]
[Route("api/v1/app-conversations")]
public class AppConversationsController : ControllerBase
{
    private readonly IAppConversationStartService _startService;
    private readonly IAppConversationInfoService _infoService;
    private readonly ILogger<AppConversationsController> _logger;

    public AppConversationsController(
        IAppConversationStartService startService,
        IAppConversationInfoService infoService,
        ILogger<AppConversationsController> logger)
    {
        _startService = startService;
        _infoService = infoService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppConversationDto>>> GetConversationsAsync(
        [FromQuery(Name = "ids")] IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return Ok(Array.Empty<AppConversationDto>());
        }

        if (ids.Count > 100)
        {
            return BadRequest(new { error = "A maximum of 100 conversation ids may be requested at once." });
        }

        IReadOnlyList<AppConversationDto> conversations = await _infoService
            .GetByIdsAsync(ids, GetUserId(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(conversations);
    }

    [HttpGet("search")]
    public async Task<ActionResult<AppConversationPageDto>> SearchConversationsAsync(
        [FromQuery(Name = "title__contains")] string titleContains,
        [FromQuery(Name = "created_at__gte")] DateTimeOffset? createdAtGte,
        [FromQuery(Name = "created_at__lt")] DateTimeOffset? createdAtLt,
        [FromQuery(Name = "updated_at__gte")] DateTimeOffset? updatedAtGte,
        [FromQuery(Name = "updated_at__lt")] DateTimeOffset? updatedAtLt,
        [FromQuery(Name = "sort_order")] string sortOrder,
        [FromQuery(Name = "page_id")] string pageId,
        [FromQuery(Name = "limit")] int? limit,
        CancellationToken cancellationToken)
    {
        AppConversationPageDto result = await _infoService
            .SearchAsync(
                new AppConversationSearchRequest
                {
                    TitleContains = titleContains,
                    CreatedAtGte = createdAtGte,
                    CreatedAtLt = createdAtLt,
                    UpdatedAtGte = updatedAtGte,
                    UpdatedAtLt = updatedAtLt,
                    SortOrder = sortOrder,
                    PageId = pageId,
                    Limit = limit,
                    UserId = GetUserId(),
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> CountConversationsAsync(
        [FromQuery(Name = "title__contains")] string titleContains,
        [FromQuery(Name = "created_at__gte")] DateTimeOffset? createdAtGte,
        [FromQuery(Name = "created_at__lt")] DateTimeOffset? createdAtLt,
        [FromQuery(Name = "updated_at__gte")] DateTimeOffset? updatedAtGte,
        [FromQuery(Name = "updated_at__lt")] DateTimeOffset? updatedAtLt,
        CancellationToken cancellationToken)
    {
        int count = await _infoService
            .CountAsync(
                new AppConversationCountRequest
                {
                    TitleContains = titleContains,
                    CreatedAtGte = createdAtGte,
                    CreatedAtLt = createdAtLt,
                    UpdatedAtGte = updatedAtGte,
                    UpdatedAtLt = updatedAtLt,
                    UserId = GetUserId(),
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(count);
    }

    [HttpPost]
    public async Task<ActionResult<AppConversationStartTaskDto>> StartConversationAsync(
        [FromBody] AppConversationStartRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received POST /api/v1/app-conversations request. Repository={Repository}; Branch={Branch}; GitProvider={GitProvider}; Title={Title}; CreatedBy={CreatedBy}",
            request?.SelectedRepository,
            request?.SelectedBranch,
            request?.GitProvider,
            request?.Title,
            request?.CreatedByUserId);
        AppConversationStartTaskDto task = await _startService.StartAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Start conversation request enqueued task {TaskId} with initial status {Status}",
            task.Id,
            task.Status);
        return Ok(task);
    }

    [HttpPost("stream-start")]
    public IAsyncEnumerable<AppConversationStartTaskDto> StreamStartAsync(
        [FromBody] AppConversationStartRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received STREAM start request for repository {Repository} and branch {Branch}",
            request?.SelectedRepository,
            request?.SelectedBranch);
        return _startService.StreamStartAsync(request, cancellationToken);
    }

    [HttpGet("start-tasks")]
    public async Task<ActionResult<IReadOnlyList<AppConversationStartTaskDto>>> GetStartTasksAsync(
        [FromQuery(Name = "ids")] IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return Ok(Array.Empty<AppConversationStartTaskDto>());
        }

        IReadOnlyList<AppConversationStartTaskDto> tasks = await _startService
            .BatchGetAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        return Ok(tasks);
    }

    [HttpGet("start-tasks/search")]
    public async Task<ActionResult<AppConversationStartTaskPageDto>> SearchStartTasksAsync(
        [FromQuery(Name = "limit")] int limit = 20,
        [FromQuery(Name = "page_id")] string pageId = null,
        CancellationToken cancellationToken = default)
    {
        AppConversationStartTaskPageDto page = await _startService
            .SearchAsync(limit, pageId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(page);
    }

    [HttpGet("start-tasks/count")]
    public async Task<ActionResult<int>> CountStartTasksAsync(
        [FromQuery(Name = "conversation_id__eq")] Guid? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        int count = await _startService
            .CountAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(count);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
