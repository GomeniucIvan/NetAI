using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Conversations;

public class FileUploadSuccessResponseDto
{
    [JsonPropertyName("uploaded_files")]
    public IReadOnlyList<string> UploadedFiles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("skipped_files")]
    public IReadOnlyList<SkippedFileDto> SkippedFiles { get; set; } = Array.Empty<SkippedFileDto>();
}

public class SkippedFileDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}
