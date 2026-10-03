/// MCP adapter over the protocol: in-process host + MCP C# SDK client on /mcp.
module Logistics.Api.Tests.Adapters.McpTests

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc.Testing
open ModelContextProtocol.Client
open ModelContextProtocol.Protocol
open Swensen.Unquote
open Xunit

let private ct () = TestContext.Current.CancellationToken

/// Runs `f` with an MCP client connected to /mcp of a fresh in-process host.
let private withMcp (f: McpClient -> Task<'a>) : Task<'a> =
    task {
        use factory = new WebApplicationFactory<Logistics.Api.Program.Marker>()
        use http = factory.CreateClient()

        let options =
            HttpClientTransportOptions(
                Endpoint = Uri(http.BaseAddress, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            )

        use transport = new HttpClientTransport(options, http, null, false)
        let! client = McpClient.CreateAsync(transport, null, null, ct ())
        use _ = client
        return! f client
    }

let private readTools =
    [ "find_dispatch_options"; "get_driver_schedule"; "get_drivers"; "list_open_orders" ]

[<Fact>]
let ``tools/list lists every tool, each with a description`` () =
    withMcp (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())
            let described = tools |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace t.Description))

            test
                <@
                    described |> Seq.map _.Name |> Seq.sort |> List.ofSeq = List.sort ("dispatch_order" :: readTools)
                @>
        })

/// Names in the tool's input schema `required` array.
let private required (tool: McpClientTool) =
    match tool.JsonSchema.TryGetProperty "required" with
    | true, names -> names.EnumerateArray() |> Seq.map _.GetString() |> List.ofSeq
    | _ -> []

[<Fact>]
let ``tools/list marks only mandatory params as required`` () =
    withMcp (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())
            let requiredOf name = tools |> Seq.find (fun t -> t.Name = name) |> required

            test <@ requiredOf "list_open_orders" = [] @>
            test <@ requiredOf "find_dispatch_options" = [ "orderId" ] @>
            test <@ requiredOf "get_driver_schedule" = [ "driverId"; "from"; "to" ] @>
            test <@ requiredOf "get_drivers" = [] @>
            test <@ requiredOf "dispatch_order" = [ "orderId"; "driverId"; "tractorId"; "trailerId" ] @>
        })

[<Fact>]
let ``tools/list annotates the read tools as read-only`` () =
    withMcp (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())

            let readOnly =
                tools
                |> Seq.filter (fun t ->
                    not (isNull t.ProtocolTool.Annotations)
                    && t.ProtocolTool.Annotations.ReadOnlyHint = Nullable true)
                |> Seq.map _.Name
                |> Seq.sort
                |> List.ofSeq

            test <@ readOnly = readTools @>
        })

let private textOf (result: CallToolResult) =
    result.Content |> Seq.exactlyOne :?> TextContentBlock |> _.Text

[<Fact>]
let ``find_dispatch_options returns ORD-103's single option as JSON`` () =
    withMcp (fun client ->
        task {
            let args = dict [ "orderId", box "ORD-103" ] |> Collections.Generic.Dictionary
            let! result = client.CallToolAsync("find_dispatch_options", args, cancellationToken = ct ())
            use json = JsonDocument.Parse(textOf result)

            let options =
                json.RootElement.GetProperty("options").EnumerateArray()
                |> Seq.map (fun o ->
                    o.GetProperty("driverId").GetString(),
                    o.GetProperty("tractorId").GetString(),
                    o.GetProperty("trailerId").GetString())
                |> List.ofSeq

            test <@ result.IsError <> Nullable true @>
            test <@ options = [ "D-01", "TRK-02", "TRL-02" ] @>
        })

let private call (client: McpClient) name (args: (string * obj) list) =
    client.CallToolAsync(name, dict args |> Collections.Generic.Dictionary, cancellationToken = ct ())

let private dispatchArgs orderId driverId tractorId trailerId : (string * obj) list =
    [ "orderId", box orderId
      "driverId", box driverId
      "tractorId", box tractorId
      "trailerId", box trailerId ]

[<Fact>]
let ``dispatch_order returns a domain error as tool text, not a protocol error`` () =
    withMcp (fun client ->
        task {
            let! result = call client "dispatch_order" (dispatchArgs "ORD-105" "D-04" "TRK-03" "TRL-06")
            let text = textOf result

            test <@ result.IsError = Nullable true @>
            test <@ text.StartsWith "Can't dispatch ORD-105:" @>
            test <@ text.Contains "Driver CPC expired" @>
        })

[<Fact>]
let ``dispatch_order names the Dispatcher and get_driver_schedule shows the new Dispatch`` () =
    withMcp (fun client ->
        task {
            let! dispatched = call client "dispatch_order" (dispatchArgs "ORD-103" "D-01" "TRK-02" "TRL-02")

            let! schedule =
                call
                    client
                    "get_driver_schedule"
                    [ "driverId", box "D-01"
                      "from", box (DateTimeOffset.UtcNow.AddDays(-1.0))
                      "to", box (DateTimeOffset.UtcNow.AddDays 3.0) ]

            use json = JsonDocument.Parse(textOf schedule)

            let orderIds =
                json.RootElement.EnumerateArray()
                |> Seq.map _.GetProperty("orderId").GetString()
                |> List.ofSeq

            test <@ dispatched.IsError <> Nullable true @>
            test <@ (textOf dispatched).StartsWith "Dispatched ORD-103 as DSP-005" @>
            test <@ (textOf dispatched).EndsWith "dispatched by anonymous." @>
            test <@ orderIds = [ "ORD-103" ] @>
        })

[<Fact>]
let ``logistics://rules reads as markdown listing the 5 rules`` () =
    withMcp (fun client ->
        task {
            let! result = client.ReadResourceAsync("logistics://rules", cancellationToken = ct ())
            let contents = result.Contents |> Seq.exactlyOne :?> TextResourceContents

            let numbered =
                contents.Text.Split('\n')
                |> Seq.filter (fun l -> l.Length > 2 && Char.IsDigit l[0] && l[1] = '.')
                |> Seq.length

            test <@ contents.MimeType = "text/markdown" @>
            test <@ contents.Text.StartsWith "# Dispatch rules" @>
            test <@ numbered = 5 @>
        })
