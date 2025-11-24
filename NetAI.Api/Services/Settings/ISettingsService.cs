using System.Threading;
using System.Threading.Tasks;
using NetAI.Api.Models.Settings;

namespace NetAI.Api.Services.Settings;

public interface ISettingsService
{
    Task<SettingsQueryResult<ApiSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<SettingsOperationResult> StoreSettingsAsync(UpdateSettingsRequestDto request, CancellationToken cancellationToken = default);
}
