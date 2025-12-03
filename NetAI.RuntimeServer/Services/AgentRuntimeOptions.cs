using System.Collections.Generic;

namespace NetAI.RuntimeServer.Services
{
    public sealed class AgentRuntimeOptions
    {
        public string Provider { get; set; } = "openhands";

        public bool EnableTooling { get; set; } = true;

        public IList<string> ToolAllowList { get; set; } = new List<string>();

        public IList<string> ToolDenyList { get; set; } = new List<string>();

        public IList<string> MicroagentRegistryPaths { get; set; } = new List<string>();

        public bool UseInMemoryStub { get; set; } = true;
    }
}
