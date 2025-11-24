using System;
using NetAI.Api.Models.Conversations;

namespace NetAI.Api.Services.Conversations;

public interface IAppConversationStartService
{
    Task<AppConversationStartTaskDto> StartAsync(AppConversationStartRequestDto request, CancellationToken cancellationToken);

    IAsyncEnumerable<AppConversationStartTaskDto> StreamStartAsync(AppConversationStartRequestDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppConversationStartTaskDto>> BatchGetAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task<AppConversationStartTaskPageDto> SearchAsync(int limit, string pageId, CancellationToken cancellationToken);

    Task<int> CountAsync(Guid? conversationId, CancellationToken cancellationToken);
}
