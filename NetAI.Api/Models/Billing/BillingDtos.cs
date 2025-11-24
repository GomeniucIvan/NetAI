using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Billing;

public record class SubscriptionAccessDto
{
    [JsonPropertyName("start_at")]
    [Required]
    public DateTimeOffset StartAt { get; init; }
        = DateTimeOffset.MinValue;

    [JsonPropertyName("end_at")]
    [Required]
    public DateTimeOffset EndAt { get; init; }
        = DateTimeOffset.MinValue;

    [JsonPropertyName("created_at")]
    [Required]
    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.MinValue;

    [JsonPropertyName("cancelled_at")]
    public DateTimeOffset? CancelledAt { get; init; }
        = null;

    [JsonPropertyName("stripe_subscription_id")]
    public string StripeSubscriptionId { get; init; }
}

public record class CancelSubscriptionResponseDto
{
    [JsonPropertyName("status")]
    [Required]
    public string Status { get; init; } 

    [JsonPropertyName("message")]
    [Required]
    public string Message { get; init; }
}

public record class CreateCheckoutSessionRequestDto
{
    [JsonPropertyName("amount")]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }
        = 0m;
}

public record class BillingSessionResponseDto
{
    [JsonPropertyName("redirect_url")]
    public string RedirectUrl { get; init; }
}

public record class CreditsResponseDto
{
    [JsonPropertyName("credits")]
    [Required]
    public string Credits { get; init; } = "0";
}
