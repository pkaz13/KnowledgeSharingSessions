/// `McpAuth:Mode = OAuth`: /mcp needs a bearer token from Keycloak; `dispatch_order` needs role `dispatcher`.
/// No Keycloak here: the 401 challenge and PRM need no token, and roles are tested with a test authentication scheme.
module Logistics.Api.Tests.Adapters.OAuthTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Security.Claims
open System.Text.Encodings.Web
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open ModelContextProtocol.Client
open ModelContextProtocol.Protocol
open Swensen.Unquote
open Xunit

let private ct () = TestContext.Current.CancellationToken

/// Browser-facing URLs from the AppHost. Nothing listens on the issuer in tests.
let private issuer = "http://localhost:8080/realms/mcp"
let private resource = "http://localhost:5080/mcp"

let private oauthHost () =
    (new WebApplicationFactory<Logistics.Api.Program.Marker>())
        .WithWebHostBuilder(fun b ->
            b
                .UseSetting("McpAuth:Mode", "OAuth")
                .UseSetting("McpAuth:Issuer", issuer)
                .UseSetting("McpAuth:ResourceUrl", resource)
            |> ignore)

let private initialize () =
    let body =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}"""

    let request =
        new HttpRequestMessage(HttpMethod.Post, "/mcp", Content = new StringContent(body, Encoding.UTF8, "application/json"))

    request.Headers.Accept.ParseAdd "application/json"
    request.Headers.Accept.ParseAdd "text/event-stream"
    request

[<Fact>]
let ``Scene 6: OAuth /mcp without a token is 401 with WWW-Authenticate pointing at the PRM`` () =
    task {
        use factory = oauthHost ()
        use http = factory.CreateClient()
        use! response = http.SendAsync(initialize (), ct ())

        let challenge = response.Headers.WwwAuthenticate |> Seq.map string |> String.concat ", "

        test <@ response.StatusCode = HttpStatusCode.Unauthorized @>
        test <@ challenge.StartsWith "Bearer" @>
        test <@ challenge.Contains "resource_metadata=" @>
        test <@ challenge.Contains "/.well-known/oauth-protected-resource" @>
    }

[<Fact>]
let ``Scene 6: OAuth serves Protected Resource Metadata naming Keycloak as the authorization server`` () =
    task {
        use factory = oauthHost ()
        use http = factory.CreateClient()
        use! response = http.GetAsync("/.well-known/oauth-protected-resource", ct ())
        let! body = response.Content.ReadAsStringAsync(ct ())
        use json = JsonDocument.Parse body

        let strings (name: string) =
            json.RootElement.GetProperty(name).EnumerateArray() |> Seq.map _.GetString() |> List.ofSeq

        test <@ response.StatusCode = HttpStatusCode.OK @>
        test <@ json.RootElement.GetProperty("resource").GetString() = resource @>
        test <@ strings "authorization_servers" = [ issuer ] @>
        test <@ strings "scopes_supported" = [ "mcp:tools" ] @>
    }

/// Stands in for Keycloak: records what JwtBearer asks for and answers 503.
/// Well-formed JWT for alice with a fake signature: JwtBearer needs Keycloak's keys to reject it.
let private unsignedAliceToken =
    "eyJhbGciOiAiUlMyNTYiLCAidHlwIjogIkpXVCIsICJraWQiOiAiayJ9."
    + "eyJpc3MiOiAiaHR0cDovL2xvY2FsaG9zdDo4MDgwL3JlYWxtcy9tY3AiLCAiYXVkIjogImh0dHA6Ly9sb2NhbGhvc3Q6NTA4MC9tY3AiLCAicHJlZmVycmVkX3VzZXJuYW1lIjogImFsaWNlIiwgImV4cCI6IDQxMDI0NDQ4MDB9."
    + "c2ln"

type private RecordingBackchannel() =
    inherit HttpMessageHandler()
    member val Requests = Collections.Concurrent.ConcurrentQueue<string>()

    override this.SendAsync(request, _) =
        this.Requests.Enqueue(string request.RequestUri)
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))

[<Fact>]
let ``OAuth: JwtBearer doesn't call Keycloak at startup, only on the first token`` () =
    task {
        let keycloak = new RecordingBackchannel()

        use factory =
            oauthHost()
                .WithWebHostBuilder(fun b ->
                    b.ConfigureTestServices(fun services ->
                        services.Configure<JwtBearerOptions>(
                            JwtBearerDefaults.AuthenticationScheme,
                            fun (o: JwtBearerOptions) -> o.BackchannelHttpHandler <- keycloak
                        )
                        |> ignore)
                    |> ignore)

        use http = factory.CreateClient()
        use! challenged = http.SendAsync(initialize (), ct ())
        use! prm = http.GetAsync("/.well-known/oauth-protected-resource", ct ())
        let beforeToken = List.ofSeq keycloak.Requests

        use withToken = initialize ()
        withToken.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", unsignedAliceToken)
        use! rejected = http.SendAsync(withToken, ct ())

        test <@ challenged.StatusCode = HttpStatusCode.Unauthorized && prm.StatusCode = HttpStatusCode.OK @>
        test <@ beforeToken = [] @>
        test <@ rejected.StatusCode = HttpStatusCode.Unauthorized @>
        test <@ keycloak.Requests |> Seq.exists (fun u -> u.StartsWith issuer) @>
    }

/// Test authentication scheme next to JwtBearer: stands in for a Keycloak token.
/// `X-Test-User: alice` gives `preferred_username` = alice and, with `X-Test-Roles`, flat `roles` claims, like the realm.
module TestScheme =
    [<Literal>]
    let Name = "Test"

    type Handler(options: IOptionsMonitor<AuthenticationSchemeOptions>, logger: ILoggerFactory, encoder: UrlEncoder) =
        inherit AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)

        override this.HandleAuthenticateAsync() =
            let result =
                match this.Request.Headers.TryGetValue "X-Test-User" with
                | true, user ->
                    let roles =
                        match this.Request.Headers.TryGetValue "X-Test-Roles" with
                        | true, r -> (string r).Split(',', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                        | _ -> []

                    let claims =
                        Claim("preferred_username", string user) :: [ for r in roles -> Claim("roles", r) ]

                    let identity = ClaimsIdentity(claims, Name, "preferred_username", "roles")
                    AuthenticateResult.Success(AuthenticationTicket(ClaimsPrincipal identity, Name))
                | _ -> AuthenticateResult.NoResult()

            Task.FromResult result

/// OAuth host where requests authenticate with the test scheme instead of a Keycloak token.
let private oauthHostWithTestScheme () =
    oauthHost()
        .WithWebHostBuilder(fun b ->
            b.ConfigureTestServices(fun services ->
                services
                    .AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestScheme.Handler>(TestScheme.Name, ignore)
                |> ignore

                services.PostConfigure<AuthenticationOptions>(fun (o: AuthenticationOptions) ->
                    o.DefaultAuthenticateScheme <- TestScheme.Name)
                |> ignore)
            |> ignore)

/// Runs `f` with an MCP client on /mcp, signed in as `user` with `roles`.
let private withMcpAs (user: string) (roles: string list) (f: McpClient -> Task<'a>) : Task<'a> =
    task {
        use factory = oauthHostWithTestScheme ()
        use http = factory.CreateClient()
        http.DefaultRequestHeaders.Add("X-Test-User", user)
        http.DefaultRequestHeaders.Add("X-Test-Roles", String.Join(",", roles))

        let options =
            HttpClientTransportOptions(Endpoint = Uri(http.BaseAddress, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp)

        use transport = new HttpClientTransport(options, http, null, false)
        let! client = McpClient.CreateAsync(transport, null, null, ct ())
        use _ = client
        return! f client
    }

let private dispatchOrd103 (client: McpClient) =
    let args =
        dict
            [ "orderId", box "ORD-103"
              "driverId", box "D-01"
              "tractorId", box "TRK-02"
              "trailerId", box "TRL-02" ]
        |> Collections.Generic.Dictionary

    client.CallToolAsync("dispatch_order", args, cancellationToken = ct ())

[<Fact>]
let ``Scene 6: alice (role dispatcher) dispatches and is named the Dispatcher`` () =
    withMcpAs "alice" [ "dispatcher" ] (fun client ->
        task {
            let! result = dispatchOrd103 client
            let text = (result.Content |> Seq.exactlyOne :?> TextContentBlock).Text

            test <@ result.IsError <> Nullable true @>
            test <@ text.EndsWith "dispatched by alice." @>
        })

/// AddAuthorizationFilters filters tools/list by authorization: bob doesn't see dispatch_order at all.
[<Fact>]
let ``Scene 6: bob (no role) doesn't see dispatch_order in tools/list, only the read tools`` () =
    withMcpAs "bob" [] (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())
            let names = tools |> Seq.map _.Name |> Seq.sort |> List.ofSeq

            test <@ names = [ "find_dispatch_options"; "get_driver_schedule"; "get_drivers"; "list_open_orders" ] @>
        })

/// Calling it by name anyway is a JSON-RPC error (not a tool result), and nothing is dispatched.
[<Fact>]
let ``Scene 6: bob (no role) calling dispatch_order by name is denied and nothing is dispatched`` () =
    withMcpAs "bob" [] (fun client ->
        task {
            let! denied =
                task {
                    try
                        let! _ = dispatchOrd103 client
                        return None
                    with :? ModelContextProtocol.McpProtocolException as e ->
                        return Some(e.ErrorCode, e.Message)
                }

            let! openOrders = client.CallToolAsync("list_open_orders", cancellationToken = ct ())
            let openText = (openOrders.Content |> Seq.exactlyOne :?> TextContentBlock).Text

            test <@ denied = Some(ModelContextProtocol.McpErrorCode.InvalidRequest, "Request failed (remote): Access forbidden: This tool requires authorization.") @>
            test <@ openText.Contains "\"ORD-103\"" @>
        })
