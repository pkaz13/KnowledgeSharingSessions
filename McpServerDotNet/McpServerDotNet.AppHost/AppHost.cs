var builder = DistributedApplication.CreateBuilder(args);

// McpAuth:Mode comes from the launch profile (None | ApiKey | OAuth).
var authMode = builder.Configuration["McpAuth:Mode"] ?? "None";

builder.AddProject<Projects.Logistics_Api>("logistics-api")
    .WithEnvironment("McpAuth__Mode", authMode)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
