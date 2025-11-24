using System.Threading;
using System.Threading.Tasks;
using NetAI.Api.Models.Installation;

namespace NetAI.Api.Services.Installation;

public interface IInstallationService
{
    Task<InstallStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<InstallationResult> InstallAsync(InstallRequestDto request, CancellationToken cancellationToken = default);
}
