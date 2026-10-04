/// `McpAuth:Mode = ApiKey`: /mcp needs the shared key in `X-Api-Key`; REST stays open.
module Logistics.Api.Tests.Adapters.ApiKeyTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc.Testing
open ModelContextProtocol.Client
open ModelContextProtocol.Protocol
open Swensen.Unquote
open Xunit

let private ct () = TestContext.Current.CancellationToken

let private apiKey = "test-key"

let private apiKeyHost () =
    (new WebApplicationFactory<Logistics.Api.Program.Marker>())
        .WithWebHostBuilder(fun b ->
            b.UseSetting("McpAuth:Mode", "ApiKey").UseSetting("McpAuth:ApiKey", apiKey) |> ignore)

let private initialize () =
    let body =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}"""

    let request =
        new HttpRequestMessage(HttpMethod.Post, "/mcp", Content = new StringContent(body, Encoding.UTF8, "application/json"))

    request.Headers.Accept.ParseAdd "application/json"
    request.Headers.Accept.ParseAdd "text/event-stream"
    request

[<Fact>]
let ``ApiKey: /mcp without a key is 401`` () =
    task {
        use factory = apiKeyHost ()
        use http = factory.CreateClient()
        use! response = http.SendAsync(initialize (), ct ())

        test <@ response.StatusCode = HttpStatusCode.Unauthorized @>
    }

[<Fact>]
let ``ApiKey: /mcp with a wrong key is 401`` () =
    task {
        use factory = apiKeyHost ()
        use http = factory.CreateClient()
        use request = initialize ()
        request.Headers.Add("X-Api-Key", "wrong-key")
        use! response = http.SendAsync(request, ct ())

        test <@ response.StatusCode = HttpStatusCode.Unauthorized @>
    }

[<Fact>]
let ``ApiKey: REST stays open without a key`` () =
    task {
        use factory = apiKeyHost ()
        use http = factory.CreateClient()
        use! response = http.GetAsync("/orders/ORD-103", ct ())

        test <@ response.StatusCode = HttpStatusCode.OK @>
    }

/// Runs `f` with an MCP client on /mcp that sends `X-Api-Key: key`.
let private withMcpKey (key: string) (f: McpClient -> Task<'a>) : Task<'a> =
    task {
        use factory = apiKeyHost ()
        use http = factory.CreateClient()
        http.DefaultRequestHeaders.Add("X-Api-Key", key)

        let options =
            HttpClientTransportOptions(Endpoint = Uri(http.BaseAddress, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp)

        use transport = new HttpClientTransport(options, http, null, false)
        let! client = McpClient.CreateAsync(transport, null, null, ct ())
        use _ = client
        return! f client
    }

[<Fact>]
let ``ApiKey: with the key, tools/list succeeds`` () =
    withMcpKey apiKey (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())

            test <@ tools |> Seq.exists (fun t -> t.Name = "dispatch_order") @>
        })

[<Fact>]
let ``Scene 7: ApiKey dispatch_order names the Dispatcher api-key`` () =
    withMcpKey apiKey (fun client ->
        task {
            let args =
                dict
                    [ "orderId", box "ORD-103"
                      "driverId", box "D-01"
                      "tractorId", box "TRK-02"
                      "trailerId", box "TRL-02" ]
                |> Collections.Generic.Dictionary

            let! result = client.CallToolAsync("dispatch_order", args, cancellationToken = ct ())
            let text = (result.Content |> Seq.exactlyOne :?> TextContentBlock).Text

            test <@ result.IsError <> Nullable true @>
            test <@ text.EndsWith "dispatched by api-key." @>
        })
