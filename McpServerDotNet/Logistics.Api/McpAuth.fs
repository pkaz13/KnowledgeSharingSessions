/// Auth for /mcp, switched by `McpAuth:Mode` (None | ApiKey | OAuth). REST stays open in every mode.
module Logistics.Api.McpAuth

open System
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.IdentityModel.Tokens
open ModelContextProtocol.AspNetCore.Authentication
open ModelContextProtocol.Authentication
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

[<RequireQualifiedAccess>]
type Mode =
    | None
    | ApiKey
    | OAuth

/// Reads `McpAuth:Mode`; missing means `None`, anything unknown fails startup.
let modeOf (config: IConfiguration) =
    match config["McpAuth:Mode"] with
    | null
    | "" -> Mode.None
    | m when m.Equals("None", StringComparison.OrdinalIgnoreCase) -> Mode.None
    | m when m.Equals("ApiKey", StringComparison.OrdinalIgnoreCase) -> Mode.ApiKey
    | m when m.Equals("OAuth", StringComparison.OrdinalIgnoreCase) -> Mode.OAuth
    | m -> failwith $"Unknown McpAuth:Mode '{m}'. Use None, ApiKey or OAuth."

module ApiKey =
    [<Literal>]
    let Scheme = "ApiKey"

    [<Literal>]
    let Header = "X-Api-Key"

    /// Name of the authenticated principal, shown as the Dispatcher.
    [<Literal>]
    let PrincipalName = "api-key"

    type Options() =
        inherit AuthenticationSchemeOptions()
        member val Key = "" with get, set

    let private matches (expected: string) (actual: string) =
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes expected, Encoding.UTF8.GetBytes actual)

    /// Compares `X-Api-Key` with the configured key.
    type Handler(options: IOptionsMonitor<Options>, logger: ILoggerFactory, encoder: UrlEncoder) =
        inherit AuthenticationHandler<Options>(options, logger, encoder)

        // Protected members can't be used inside a `task {}` closure in F#, and nothing here awaits.
        override this.HandleAuthenticateAsync() =
            let result =
                match this.Request.Headers.TryGetValue Header with
                | true, value when matches this.Options.Key (string value) ->
                    let identity =
                        ClaimsIdentity(
                            [ Claim(ClaimTypes.Name, PrincipalName); Claim(ClaimTypes.Role, Dispatcher.Role) ],
                            Scheme
                        )

                    AuthenticateResult.Success(AuthenticationTicket(ClaimsPrincipal identity, Scheme))
                | true, _ -> AuthenticateResult.Fail "Invalid API key."
                | _ -> AuthenticateResult.NoResult()

            Threading.Tasks.Task.FromResult result

    let addServices (builder: WebApplicationBuilder) =
        let key = builder.Configuration["McpAuth:ApiKey"]

        if String.IsNullOrWhiteSpace key then
            failwith "McpAuth:Mode is ApiKey but McpAuth:ApiKey is not set (Aspire parameter mcp-api-key)."

        builder.Services
            .AddAuthentication(Scheme)
            .AddScheme<Options, Handler>(Scheme, fun o -> o.Key <- key)
        |> ignore

module OAuth =
    let private required (config: IConfiguration) key =
        match config[key] with
        | v when String.IsNullOrWhiteSpace v -> failwith $"McpAuth:Mode is OAuth but {key} is not set (the AppHost sets it)."
        | v -> v

    /// Bearer tokens from Keycloak (JwtBearer) + the MCP challenge and Protected Resource Metadata (AddMcp).
    /// JwtBearer fetches Keycloak's metadata lazily, on the first token, so the API starts without Keycloak.
    let addServices (builder: WebApplicationBuilder) =
        // Browser-facing URLs: must equal the token's `iss` and `aud` (the Audience mapper sets `aud`).
        let issuer = required builder.Configuration "McpAuth:Issuer"
        let resource = required builder.Configuration "McpAuth:ResourceUrl"

        builder.Services
            .AddAuthentication(fun o ->
                o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
                // 401 with `WWW-Authenticate: Bearer resource_metadata="..."`.
                o.DefaultChallengeScheme <- McpAuthenticationDefaults.AuthenticationScheme)
            .AddJwtBearer(fun o ->
                o.Authority <- issuer
                o.RequireHttpsMetadata <- false // local Keycloak on http: demo only
                o.MapInboundClaims <- false // keep `preferred_username` and `roles` as they are
                o.TokenValidationParameters <-
                    TokenValidationParameters(
                        ValidIssuer = issuer,
                        ValidAudience = resource,
                        NameClaimType = "preferred_username",
                        // Flat `roles` claim from the realm's role mapper.
                        RoleClaimType = "roles"
                    ))
            .AddMcp(fun o ->
                o.ResourceMetadata <-
                    ProtectedResourceMetadata(
                        Resource = resource,
                        AuthorizationServers = ResizeArray [ issuer ],
                        ScopesSupported = ResizeArray [ "mcp:tools" ]
                    ))
        |> ignore

/// Auth off: every authorization requirement passes, so `[Authorize(Roles = "dispatcher")]` on
/// `dispatch_order` doesn't block the anonymous caller.
type AllowAllHandler() =
    interface IAuthorizationHandler with
        member _.HandleAsync context =
            for requirement in List.ofSeq context.PendingRequirements do
                context.Succeed requirement

            Threading.Tasks.Task.CompletedTask

/// Registers authentication and authorization for the mode.
let addServices mode (builder: WebApplicationBuilder) =
    match mode with
    | Mode.None ->
        builder.Services.AddAuthorization() |> ignore
        builder.Services.AddSingleton<IAuthorizationHandler, AllowAllHandler>() |> ignore
    | Mode.ApiKey ->
        ApiKey.addServices builder
        builder.Services.AddAuthorization() |> ignore
    | Mode.OAuth ->
        OAuth.addServices builder
        builder.Services.AddAuthorization() |> ignore

/// Maps /mcp, protected unless the mode is None.
let mapMcp mode (app: WebApplication) =
    let mcp = app.MapMcp("/mcp")

    match mode with
    | Mode.None -> ()
    | Mode.ApiKey
    | Mode.OAuth -> mcp.RequireAuthorization() |> ignore
