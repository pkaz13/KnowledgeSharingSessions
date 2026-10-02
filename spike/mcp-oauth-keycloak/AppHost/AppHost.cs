var builder = DistributedApplication.CreateBuilder(args);

var authEnabled = bool.TryParse(builder.Configuration["McpAuth:Enabled"], out var enabled) && enabled;

var mcp = builder.AddProject<Projects.McpServer>("mcp-server");

if (authEnabled)
{
    // Fixed admin password and port: spike only, so the realm and URLs are predictable.
    var adminPassword = builder.AddParameter("keycloak-password", "admin", secret: true);
    var keycloak = builder.AddKeycloak("keycloak", 8080, adminPassword: adminPassword)
        .WithRealmImport("./Realms");

    mcp.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("McpAuth__Enabled", "true")
        .WithEnvironment("McpAuth__Issuer", "http://localhost:8080/realms/mcp")
        .WithEnvironment("McpAuth__ResourceUrl", "http://localhost:5080/mcp");
}

builder.Build().Run();
