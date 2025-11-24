using System;
using System.Collections.Generic;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public class AppConversationSearchRequest
{
    public string TitleContains { get; init; }

    public DateTimeOffset? CreatedAtGte { get; init; }

    public DateTimeOffset? CreatedAtLt { get; init; }

    public DateTimeOffset? UpdatedAtGte { get; init; }

    public DateTimeOffset? UpdatedAtLt { get; init; }

    public string SortOrder { get; init; }

    public string PageId { get; init; }

    public int? Limit { get; init; }

    public string UserId { get; init; }
}

public class AppConversationCountRequest
{
    public string TitleContains { get; init; }

    public DateTimeOffset? CreatedAtGte { get; init; }

    public DateTimeOffset? CreatedAtLt { get; init; }

    public DateTimeOffset? UpdatedAtGte { get; init; }

    public DateTimeOffset? UpdatedAtLt { get; init; }

    public string UserId { get; init; }
}

public interface IAppConversationInfoService
{
    Task<AppConversationPageDto> SearchAsync(AppConversationSearchRequest request, CancellationToken cancellationToken);

    Task<int> CountAsync(AppConversationCountRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppConversationDto>> GetByIdsAsync(
        IReadOnlyList<Guid> conversationIds,
        string userId,
        CancellationToken cancellationToken);
}
