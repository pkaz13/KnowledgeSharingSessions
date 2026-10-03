/// Auth for /mcp, switched by `McpAuth:Mode` (None | ApiKey | OAuth). REST stays open in every mode.
module Logistics.Api.McpAuth

open System
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Builder
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

/// Role that may dispatch. The shared API key grants it: one key, full access, no per-user identity.
[<Literal>]
let DispatcherRole = "dispatcher"

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
                            [ Claim(ClaimTypes.Name, PrincipalName); Claim(ClaimTypes.Role, DispatcherRole) ],
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

/// Registers authentication and authorization for the mode.
let addServices mode (builder: WebApplicationBuilder) =
    match mode with
    | Mode.None -> ()
    | Mode.ApiKey ->
        ApiKey.addServices builder
        builder.Services.AddAuthorization() |> ignore
    | Mode.OAuth -> failwith "McpAuth:Mode OAuth is not built yet (#29)."

/// Maps /mcp, protected unless the mode is None.
let mapMcp mode (app: WebApplication) =
    let mcp = app.MapMcp("/mcp")

    match mode with
    | Mode.None -> ()
    | Mode.ApiKey
    | Mode.OAuth -> mcp.RequireAuthorization() |> ignore
