using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class ProviderTokenRecord
{
    [MaxLength(512)]
    public string Token { get; set; }

    [MaxLength(200)]
    public string UserId { get; set; }

    [MaxLength(200)]
    public string Host { get; set; }
}
