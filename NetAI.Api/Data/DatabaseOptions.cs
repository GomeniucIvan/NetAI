namespace NetAI.Api.Data;

public class DatabaseOptions
{
    public string Provider { get; set; } = "Postgres";

    public string ConnectionString { get; set; }
}
