using NetAI.Api.Models.Security;
using System.Threading;

namespace NetAI.Api.Services.Security;

public interface ISecurityService
{
    Task<SecurityQueryResult<SecurityPolicyResponseDto>> GetPolicyAsync(CancellationToken cancellationToken = default);

    Task<SecurityOperationResult> UpdatePolicyAsync(UpdateSecurityPolicyRequestDto request, CancellationToken cancellationToken = default);

    Task<SecurityQueryResult<SecurityRiskSettingsDto>> GetRiskSettingsAsync(CancellationToken cancellationToken = default);

    Task<SecurityOperationResult> UpdateRiskSettingsAsync(SecurityRiskSettingsDto request, CancellationToken cancellationToken = default);

    Task<SecurityQueryResult<SecurityTraceExportDto>> ExportTraceAsync(CancellationToken cancellationToken = default);

    Task<SecurityQueryResult<AccessTokenVerificationResultDto>> VerifyAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
