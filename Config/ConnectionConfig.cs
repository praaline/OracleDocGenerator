public class ConnectionConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1521;
    public string Service { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Schema { get; set; } = "";

    public ConnectionConfig() { }

    public ConnectionConfig(string host, int port, string service, string username, string password, string schema)
    {
        Host = host;
        Port = port;
        Service = service;
        Username = username;
        Password = password;
        Schema = schema;
    }

    public string BuildConnectionString()
    {
        return $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Host})(PORT={Port}))(CONNECT_DATA=(SERVICE_NAME={Service})));User Id={Username};Password={Password};";
    }
}
