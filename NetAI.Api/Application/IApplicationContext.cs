namespace NetAI.Api.Application;

public interface IApplicationContext
{
    bool IsInstalled { get; }
    AppConfiguration AppConfiguration { get; }
}
