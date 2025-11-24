using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Experiments;

public class ExperimentConfigDto
{
    [JsonPropertyName("config")]
    public IDictionary<string, string> Config { get; set; }
}
