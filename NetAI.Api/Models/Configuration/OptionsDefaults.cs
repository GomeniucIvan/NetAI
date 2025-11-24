using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NetAI.Api.Models.Configuration;

public static class OptionsDefaults
{
    public static readonly IReadOnlyList<string> Models = new ReadOnlyCollection<string>(new[]
    {
        "gpt-3.5-turbo",
        "gpt-4o",
        "gpt-4o-mini",
        "anthropic/claude-3.5",
        "anthropic/claude-sonnet-4-20250514",
        "anthropic/claude-sonnet-4-5-20250929",
        "anthropic/claude-haiku-4-5-20251001",
        "openhands/claude-sonnet-4-20250514",
        "openhands/claude-sonnet-4-5-20250929",
        "openhands/claude-haiku-4-5-20251001",
        "deepseek/deepseek-chat",
        "sambanova/Meta-Llama-3.1-8B-Instruct"
    });

    public static readonly IReadOnlyList<string> Agents = new ReadOnlyCollection<string>(new[]
    {
        "CodeActAgent",
        "CoActAgent"
    });

    public static readonly IReadOnlyList<string> SecurityAnalyzers = new ReadOnlyCollection<string>(new[]
    {
        "llm",
        "none"
    });
}
