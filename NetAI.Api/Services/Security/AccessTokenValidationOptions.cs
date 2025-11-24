using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Services.Security;

public class AccessTokenValidationOptions
{
    [Required]
    public string SigningKey { get; set; }

    public string ValidIssuer { get; set; }

    public string ValidAudience { get; set; }
}
