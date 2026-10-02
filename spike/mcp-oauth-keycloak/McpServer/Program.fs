open System
open System.ComponentModel
open System.Runtime.InteropServices
open System.Security.Claims
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.IdentityModel.Tokens
open ModelContextProtocol.AspNetCore.Authentication
open ModelContextProtocol.Authentication
open ModelContextProtocol.Server

[<McpServerToolType>]
module SpikeTools =
    [<McpServerTool(Name = "whoami"); Description("Returns the caller identity as seen by the MCP server.")>]
    let whoami ([<Optional; DefaultParameterValue(null: ClaimsPrincipal)>] user: ClaimsPrincipal) : string =
        if isNull user || isNull user.Identity || not user.Identity.IsAuthenticated then
            "anonymous"
        else
            let claim t = user.FindFirst(t: string) |> Option.ofObj |> Option.map _.Value |> Option.defaultValue "-"
            $"""user={claim "preferred_username"} sub={claim "sub"} aud={claim "aud"} azp={claim "azp"} scope={claim "scope"}"""

let builder = WebApplication.CreateBuilder(Environment.GetCommandLineArgs()[1..])
let authEnabled = builder.Configuration.GetValue("McpAuth:Enabled", false)
// Browser-facing URLs: must equal the token `iss` and the MCP resource URL (`aud`).
let mcpUrl = builder.Configuration["McpAuth:ResourceUrl"]
let issuer = builder.Configuration["McpAuth:Issuer"]

if authEnabled then
    builder.Services
        .AddAuthentication(fun o ->
            o.DefaultChallengeScheme <- McpAuthenticationDefaults.AuthenticationScheme
            o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun o ->
            o.Authority <- issuer
            o.RequireHttpsMetadata <- false
            o.MapInboundClaims <- false
            o.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidIssuer = issuer,
                    ValidAudience = mcpUrl,
                    NameClaimType = "preferred_username"))
        .AddMcp(fun o ->
            o.ResourceMetadata <-
                ProtectedResourceMetadata(
                    AuthorizationServers = ResizeArray [ issuer ],
                    ScopesSupported = ResizeArray [ "mcp:tools" ]))
    |> ignore
    builder.Services.AddAuthorization() |> ignore

builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly() |> ignore

let app = builder.Build()
app.MapGet("/health", Func<string>(fun () -> "ok")) |> ignore

if authEnabled then
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.MapMcp("/mcp").RequireAuthorization() |> ignore
else
    app.MapMcp("/mcp") |> ignore

app.Run()
