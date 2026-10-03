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
        // Keycloak and token validation arrive with #29.
        break;
}

builder.Build().Run();
