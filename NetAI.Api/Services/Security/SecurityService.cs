using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Conversations;
using NetAI.Api.Models.Security;
using NetAI.Api.Services.Conversations;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NetAI.Api.Services.Security;

public class SecurityService : ISecurityService
{
    private const int MinimumRiskSeverity = 0;
    private const int MaximumRiskSeverity = 3;

    private readonly ISecurityStateStore _stateStore;
    private readonly IConversationSessionService _conversationService;
    private readonly ILogger<SecurityService> _logger;
    private readonly AccessTokenValidationOptions _accessTokenOptions;

    public SecurityService(
        ISecurityStateStore stateStore,
        IConversationSessionService conversationService,
        ILogger<SecurityService> logger,
        IOptions<AccessTokenValidationOptions> accessTokenOptions = null)
    {
        _stateStore = stateStore;
        _conversationService = conversationService;
        _logger = logger;
        _accessTokenOptions = accessTokenOptions?.Value ?? new AccessTokenValidationOptions();
    }

    public async Task<SecurityQueryResult<SecurityPolicyResponseDto>> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SecurityStateRecord state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var response = new SecurityPolicyResponseDto
            {
                Policy = state.Policy ?? string.Empty
            };

            return SecurityQueryResult<SecurityPolicyResponseDto>.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load security policy");
            return SecurityQueryResult<SecurityPolicyResponseDto>.Failure(StatusCodes.Status500InternalServerError, "Failed to load security policy");
        }
    }

    public async Task<SecurityOperationResult> UpdatePolicyAsync(UpdateSecurityPolicyRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return SecurityOperationResult.Failure(StatusCodes.Status400BadRequest, "Request body is required");
        }

        string providedPolicy = request.Policy;
        if (string.IsNullOrWhiteSpace(providedPolicy))
        {
            return SecurityOperationResult.Failure(StatusCodes.Status400BadRequest, "policy is required");
        }

        try
        {
            SecurityStateRecord existing = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            SecurityStateRecord updated = existing with
            {
                Policy = providedPolicy!,
                PolicyUpdatedAt = DateTimeOffset.UtcNow
            };

            await _stateStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);
            return SecurityOperationResult.SuccessResult(StatusCodes.Status200OK, "Policy updated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update security policy");
            return SecurityOperationResult.Failure(StatusCodes.Status500InternalServerError, "Failed to update security policy");
        }
    }

    public async Task<SecurityQueryResult<SecurityRiskSettingsDto>> GetRiskSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SecurityStateRecord state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var dto = new SecurityRiskSettingsDto
            {
                RiskSeverity = state.RiskSeverity
            };

            return SecurityQueryResult<SecurityRiskSettingsDto>.SuccessResult(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load security risk settings");
            return SecurityQueryResult<SecurityRiskSettingsDto>.Failure(StatusCodes.Status500InternalServerError, "Failed to load risk settings");
        }
    }

    public async Task<SecurityOperationResult> UpdateRiskSettingsAsync(SecurityRiskSettingsDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return SecurityOperationResult.Failure(StatusCodes.Status400BadRequest, "Request body is required");
        }

        if (request.RiskSeverity < MinimumRiskSeverity || request.RiskSeverity > MaximumRiskSeverity)
        {
            return SecurityOperationResult.Failure(StatusCodes.Status400BadRequest, $"RISK_SEVERITY must be between {MinimumRiskSeverity} and {MaximumRiskSeverity}");
        }

        try
        {
            SecurityStateRecord existing = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            SecurityStateRecord updated = existing with
            {
                RiskSeverity = request.RiskSeverity,
                RiskSeverityUpdatedAt = DateTimeOffset.UtcNow
            };

            await _stateStore.StoreAsync(updated, cancellationToken).ConfigureAwait(false);
            return SecurityOperationResult.SuccessResult(StatusCodes.Status200OK, "Risk severity updated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update risk severity");
            return SecurityOperationResult.Failure(StatusCodes.Status500InternalServerError, "Failed to update risk severity");
        }
    }

    public async Task<SecurityQueryResult<SecurityTraceExportDto>> ExportTraceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SecurityStateRecord state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            List<SecurityTraceConversationDto> conversations = await LoadConversationTracesAsync(cancellationToken).ConfigureAwait(false);

            var export = new SecurityTraceExportDto
            {
                ExportedAt = DateTimeOffset.UtcNow,
                Policy = state.Policy ?? string.Empty,
                RiskSeverity = state.RiskSeverity,
                Conversations = conversations
            };

            return SecurityQueryResult<SecurityTraceExportDto>.SuccessResult(export);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to export security trace data");
            return SecurityQueryResult<SecurityTraceExportDto>.Failure(StatusCodes.Status500InternalServerError, "Failed to export trace data");
        }
    }

    public Task<SecurityQueryResult<AccessTokenVerificationResultDto>> VerifyAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.Failure(
                StatusCodes.Status401Unauthorized,
                "Access token is required."));
        }

        if (string.IsNullOrWhiteSpace(_accessTokenOptions.SigningKey))
        {
            _logger.LogWarning("Access token verification is not configured.");
            return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.Failure(
                StatusCodes.Status500InternalServerError,
                "Access token verification is not configured."));
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            byte[] keyBytes = Encoding.UTF8.GetBytes(_accessTokenOptions.SigningKey!);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateIssuer = !string.IsNullOrWhiteSpace(_accessTokenOptions.ValidIssuer),
                ValidIssuer = _accessTokenOptions.ValidIssuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(_accessTokenOptions.ValidAudience),
                ValidAudience = _accessTokenOptions.ValidAudience,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            ClaimsPrincipal principal = handler.ValidateToken(accessToken, validationParameters, out _);

            string userId = principal.FindFirstValue("user_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            string providerRaw = principal.FindFirstValue("provider_type");

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(providerRaw))
            {
                return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.Failure(
                    StatusCodes.Status401Unauthorized,
                    "Invalid access token payload."));
            }

            if (!Enum.TryParse(providerRaw, true, out ProviderType providerType))
            {
                return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.Failure(
                    StatusCodes.Status400BadRequest,
                    "Unknown provider type."));
            }

            var result = new AccessTokenVerificationResultDto
            {
                UserId = userId,
                ProviderType = providerType
            };

            return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.SuccessResult(result));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Access token validation failed.");
            return Task.FromResult(SecurityQueryResult<AccessTokenVerificationResultDto>.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }
    }

    private async Task<List<SecurityTraceConversationDto>> LoadConversationTracesAsync(CancellationToken cancellationToken)
    {
        var result = new List<SecurityTraceConversationDto>();
        string pageId = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        do
        {
            ResultSetDto<ConversationDto> page = await _conversationService
                .GetConversationsAsync(100, pageId, null, null, cancellationToken)
                .ConfigureAwait(false);

            foreach (ConversationDto conversation in page.Results)
            {
                if (string.IsNullOrWhiteSpace(conversation.ConversationId) || !seen.Add(conversation.ConversationId))
                {
                    continue;
                }

                SecurityTraceConversationDto trace = await BuildConversationTraceAsync(conversation, cancellationToken).ConfigureAwait(false);
                if (trace is not null)
                {
                    result.Add(trace);
                }
            }

            pageId = page.NextPageId;
        }
        while (!string.IsNullOrEmpty(pageId));

        return result;
    }

    private async Task<SecurityTraceConversationDto> BuildConversationTraceAsync(ConversationDto conversation, CancellationToken cancellationToken)
    {
        try
        {
            TrajectoryResponseDto trajectory = await _conversationService
                .GetTrajectoryAsync(conversation.ConversationId, conversation.SessionApiKey, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<SecurityTraceEventDto> events = trajectory.Trajectory?
                    .Select((element, index) => CreateTraceEvent(element, index))
                    .ToList()
                ?? new List<SecurityTraceEventDto>();

            return new SecurityTraceConversationDto
            {
                ConversationId = conversation.ConversationId,
                Title = conversation.Title,
                Status = conversation.Status,
                Events = events
            };
        }
        catch (ConversationSessionException ex)
        {
            _logger.LogDebug(ex, "Skipping conversation {ConversationId} during trace export", conversation.ConversationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to export trace for conversation {ConversationId}", conversation.ConversationId);
            return null;
        }
    }

    private static SecurityTraceEventDto CreateTraceEvent(JsonElement element, int index)
    {
        string type = null;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("type", out JsonElement typeProperty) && typeProperty.ValueKind == JsonValueKind.String)
        {
            type = typeProperty.GetString();
        }

        string role = null;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("role", out JsonElement roleProperty) && roleProperty.ValueKind == JsonValueKind.String)
        {
            role = roleProperty.GetString();
        }

        DateTimeOffset? createdAt = null;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("created_at", out JsonElement createdAtProperty))
        {
            if (createdAtProperty.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(createdAtProperty.GetString(), out DateTimeOffset parsed))
            {
                createdAt = parsed;
            }
            else if (createdAtProperty.ValueKind == JsonValueKind.Number && createdAtProperty.TryGetInt64(out long unixSeconds))
            {
                createdAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }

        return new SecurityTraceEventDto
        {
            Index = index,
            Type = type,
            Role = role,
            CreatedAt = createdAt,
            Event = element.Clone()
        };
    }
}
