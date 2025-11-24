using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.EventCallbacks;

public interface IEventCallbackManagementService
{
    Task<EventCallbackDto> CreateCallbackAsync(CreateEventCallbackRequestDto request, CancellationToken cancellationToken);

    Task<EventCallbackDto> GetCallbackAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteCallbackAsync(Guid id, CancellationToken cancellationToken);

    Task<EventCallbackPageDto> SearchCallbacksAsync(SearchEventCallbacksRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventCallbackDto>> BatchGetCallbacksAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    Task<EventCallbackResultDto> CreateResultAsync(CreateEventCallbackResultRequestDto request, CancellationToken cancellationToken);

    Task<EventCallbackResultDto> GetResultAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteResultAsync(Guid id, CancellationToken cancellationToken);

    Task<EventCallbackResultPageDto> SearchResultsAsync(SearchEventCallbackResultsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventCallbackResultDto>> BatchGetResultsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}
