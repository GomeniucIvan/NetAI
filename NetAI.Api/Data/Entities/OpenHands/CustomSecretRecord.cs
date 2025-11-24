using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class CustomSecretRecord
{
    [MaxLength(512)]
    public string Secret { get; set; }

    [MaxLength(512)]
    public string Description { get; set; }
}
