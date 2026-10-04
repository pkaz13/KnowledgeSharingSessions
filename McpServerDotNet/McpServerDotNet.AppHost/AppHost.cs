var builder = DistributedApplication.CreateBuilder(args);

// McpAuth:Mode comes from the launch profile (None | ApiKey | OAuth).
var authMode = builder.Configuration["McpAuth:Mode"] ?? "None";

var api = builder.AddProject<Projects.Logistics_Api>("logistics-api")
    .WithEnvironment("McpAuth__Mode", authMode)
    .WithHttpHealthCheck("/health");

switch (authMode)
{
    case "ApiKey":
        // Shared key for X-Api-Key. Set Parameters:mcp-api-key in user secrets, or enter it in the dashboard.
        var apiKey = builder.AddParameter("mcp-api-key", secret: true);
        api.WithEnvironment("McpAuth__ApiKey", apiKey);
        break;
    case "OAuth":
        // Fixed port and admin password: the issuer URL must be stable (tokens carry it), and this is a local demo.
        var adminPassword = builder.AddParameter("keycloak-password", "admin", secret: true);
        var keycloak = builder.AddKeycloak("keycloak", 8080, adminPassword: adminPassword)
            // Survives AppHost restarts, so no Keycloak start-up wait during the session.
            // The realm is imported only when the container is created: after editing the realm JSON remove the container.
            .WithLifetime(ContainerLifetime.Persistent)
            .WithRealmImport("./Realms");

        // Browser-facing URLs: the token's iss and aud must equal them.
        api.WithReference(keycloak)
            .WaitFor(keycloak)
            .WithEnvironment("McpAuth__Issuer", "http://localhost:8080/realms/mcp")
            .WithEnvironment("McpAuth__ResourceUrl", "http://localhost:5080/mcp");
        break;
}

builder.Build().Run();
