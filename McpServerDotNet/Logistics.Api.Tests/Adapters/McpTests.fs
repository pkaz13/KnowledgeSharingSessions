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
let ``tools/list lists the read tools, each with a description`` () =
    withMcp (fun client ->
        task {
            let! tools = client.ListToolsAsync(cancellationToken = ct ())
            let described = tools |> Seq.filter (fun t -> not (String.IsNullOrWhiteSpace t.Description))

            test <@ described |> Seq.map _.Name |> Seq.sort |> List.ofSeq = readTools @>
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
