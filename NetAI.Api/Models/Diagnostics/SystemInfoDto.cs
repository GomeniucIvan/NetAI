using System.Text.Json.Serialization;

namespace NetAI.Api.Models.Diagnostics;

public sealed record SystemInfoDto(
    [property: JsonPropertyName("uptime")] double Uptime,
    [property: JsonPropertyName("idle_time")] double IdleTime,
    [property: JsonPropertyName("resources")] SystemResourceStatsDto Resources);

public sealed record SystemResourceStatsDto(
    [property: JsonPropertyName("cpu_percent")] double CpuPercent,
    MemoryStatsDto Memory,
    DiskStatsDto Disk,
    IoStatsDto Io);

public sealed record MemoryStatsDto(long Rss, long Vms, double? Percent);

public sealed record DiskStatsDto(long Total, long Used, long Free, double Percent);

public sealed record IoStatsDto(
    [property: JsonPropertyName("read_bytes")] long ReadBytes,
    [property: JsonPropertyName("write_bytes")] long WriteBytes);
