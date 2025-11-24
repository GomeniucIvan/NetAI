using NetAI.Api.Models.Diagnostics;

namespace NetAI.Api.Services.Diagnostics;

public interface ISystemInfoProvider
{
    SystemInfoDto GetSystemInfo();

    void UpdateLastExecutionTime();
}
