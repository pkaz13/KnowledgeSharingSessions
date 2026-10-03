/// The files the presenter opens live (`logistics.http`, `prompts.md`) stay consistent with the API and the seed.
///
/// By default the requests run in-process. To check a running AppHost instead (e.g. before a Session):
///   DEMO_API_URL=http://localhost:5080 DEMO_API_MODE=None dotnet test --filter "FullyQualifiedName~ArtefactTests"
/// DEMO_API_MODE (None | OAuth) picks which requests of `logistics.http` run against it.
module Logistics.Api.Tests.DemoScenes.ArtefactTests

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Mvc.Testing
open Logistics.Api
open Swensen.Unquote
open Xunit

let private ct () = TestContext.Current.CancellationToken

/// The Topic folder: the first parent of the test binaries that holds `prompts.md`.
let private topicDir =
    let rec up (dir: DirectoryInfo) =
        if isNull dir then failwith "prompts.md not found above the test binaries"
        elif File.Exists(Path.Combine(dir.FullName, "prompts.md")) then dir.FullName
        else up dir.Parent

    up (DirectoryInfo AppContext.BaseDirectory)

let private read name = File.ReadAllText(Path.Combine(topicDir, name))

/// One request from `logistics.http`, with the `# expect: <status> [text in body]` and `# mode: <mode>` comments above it.
type HttpFileRequest =
    { Name: string
      Mode: string
      Method: string
      Url: string
      Headers: (string * string) list
      Body: string
      ExpectedStatus: int
      ExpectedText: string }

let private parseHttpFile (text: string) =
    let lines = text.Replace("\r\n", "\n").Split('\n')

    let variables =
        lines
        |> Array.choose (fun l ->
            let m = Regex.Match(l, @"^@(\w+)\s*=\s*(.+)$")
            if m.Success then Some(m.Groups[1].Value, m.Groups[2].Value.Trim()) else None)

    let substitute (s: string) =
        variables |> Array.fold (fun (acc: string) (k, v) -> acc.Replace($"{{{{{k}}}}}", v)) s

    let blocks =
        String.Join("\n", lines |> Array.filter (fun l -> not (l.StartsWith "@")))
        |> fun t -> Regex.Split(t, @"^###", RegexOptions.Multiline)

    [ for block in blocks do
          let blockLines = block.Split('\n') |> List.ofArray
          let name = blockLines.Head.Trim()
          let isComment (l: string) = l.TrimStart().StartsWith "#" || l.TrimStart().StartsWith "//"

          let comment key =
              blockLines
              |> List.tryPick (fun l ->
                  let m = Regex.Match(l, $@"^#\s*{key}:\s*(.+)$")
                  if m.Success then Some(m.Groups[1].Value.Trim()) else None)

          let rest = blockLines.Tail |> List.skipWhile (fun l -> isComment l || String.IsNullOrWhiteSpace l)

          match rest with
          | [] -> ()
          | requestLine :: afterRequest ->
              let parts = (substitute requestLine).Split(' ', StringSplitOptions.RemoveEmptyEntries)
              let headerLines = afterRequest |> List.takeWhile (fun l -> not (String.IsNullOrWhiteSpace l))

              let body =
                  afterRequest
                  |> List.skipWhile (fun l -> not (String.IsNullOrWhiteSpace l))
                  |> List.filter (fun l -> not (isComment l))
                  |> String.concat "\n"
                  |> _.Trim()

              let expect = comment "expect" |> Option.defaultWith (fun () -> failwith $"'{name}' has no '# expect:'")
              let status, text = match expect.Split(' ', 2) with [| s |] -> s, "" | a -> a[0], a[1]

              { Name = name
                Mode = comment "mode" |> Option.defaultValue "None"
                Method = parts[0]
                Url = parts[1]
                Headers =
                  headerLines
                  |> List.map (fun h -> let i = h.IndexOf ':' in h[.. i - 1].Trim(), substitute (h[i + 1 ..].Trim()))
                Body = substitute body
                ExpectedStatus = int status
                ExpectedText = text } ]

/// Sends the requests in file order; returns one line per mismatch.
let private run (client: HttpClient) (requests: HttpFileRequest list) =
    task {
        let failures = ResizeArray()

        for r in requests do
            use message = new HttpRequestMessage(HttpMethod r.Method, r.Url)
            let contentType = r.Headers |> List.tryFind (fun (k, _) -> k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))

            if r.Body <> "" then
                message.Content <- new StringContent(r.Body, Encoding.UTF8, contentType |> Option.map snd |> Option.defaultValue "text/plain")

            for k, v in r.Headers do
                if not (k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) then
                    message.Headers.TryAddWithoutValidation(k, v) |> ignore

            use! response = client.SendAsync(message, ct ())
            let! body = response.Content.ReadAsStringAsync(ct ())
            let status = int response.StatusCode

            if status <> r.ExpectedStatus || not (body.Contains r.ExpectedText) then
                failures.Add $"{r.Name}: expected {r.ExpectedStatus} '{r.ExpectedText}', got {status} '{body}'"

        return List.ofSeq failures
    }

let private requests = lazy (parseHttpFile (read "logistics.http"))

let private inProcess mode =
    (new WebApplicationFactory<Logistics.Api.Program.Marker>())
        .WithWebHostBuilder(fun b ->
            b
                .UseSetting("McpAuth:Mode", mode)
                .UseSetting("McpAuth:Issuer", "http://localhost:8080/realms/mcp")
                .UseSetting("McpAuth:ResourceUrl", "http://localhost:5080/mcp")
            |> ignore)

let private runMode mode =
    task {
        let selected = requests.Value |> List.filter (fun r -> r.Mode = mode)

        match Environment.GetEnvironmentVariable "DEMO_API_URL", Environment.GetEnvironmentVariable "DEMO_API_MODE" with
        | null, _ ->
            use factory = inProcess mode
            use client = factory.CreateClient()
            return! run client selected
        | _, target when (if isNull target then "None" else target) <> mode -> return []
        | url, _ ->
            // Absolute URLs in the file point at the default port; rebase them on DEMO_API_URL.
            let rebased = selected |> List.map (fun r -> { r with Url = r.Url.Replace("http://localhost:5080", url.TrimEnd '/') })
            use client = new HttpClient()
            return! run client rebased
    }

[<Fact>]
let ``logistics.http covers the read path, the dispatch path and scene 6`` () =
    let paths = requests.Value |> List.map (fun r -> r.Method + " " + Uri(r.Url).AbsolutePath)

    test <@ paths |> List.contains "GET /orders/ORD-103/dispatch-options" @>
    test <@ paths |> List.contains "POST /dispatches" @>
    test <@ paths |> List.contains "GET /dispatches" @>
    test <@ requests.Value |> List.exists (fun r -> r.Mode = "OAuth" && r.Url.EndsWith "/mcp" && r.ExpectedStatus = 401) @>
    test <@ paths |> List.contains "GET /.well-known/oauth-protected-resource" @>

[<Fact>]
let ``Every logistics.http request in mode None returns its expected status`` () =
    task {
        let! failures = runMode "None"
        test <@ failures = [] @>
    }

[<Fact>]
let ``Scene 6: every logistics.http request in mode OAuth returns its expected status`` () =
    task {
        let! failures = runMode "OAuth"
        test <@ failures = [] @>
    }

let private seed = Seed.create DateTimeOffset.UtcNow
let private prompts = lazy (read "prompts.md")

[<Fact>]
let ``Every Order, Driver, Tractor unit and Trailer ID in prompts.md exists in the seed`` () =
    let known =
        Set.ofList (
            (seed.Orders |> List.map _.Id)
            @ (seed.Drivers |> List.map _.Id)
            @ (seed.Tractors |> List.map _.Id)
            @ (seed.Trailers |> List.map _.Id)
        )

    let used =
        Regex.Matches(prompts.Value, @"\b(ORD-\d{3}|D-\d{2}|TRK-\d{2}|TRL-\d{2})\b")
        |> Seq.map _.Value
        |> Set.ofSeq

    test <@ not used.IsEmpty @>
    test <@ Set.difference used known = Set.empty @>

[<Fact>]
let ``Every "Name (D-xx)" in prompts.md names the seed's driver`` () =
    let pairs =
        Regex.Matches(prompts.Value, @"([A-Z][a-z]+ [A-Z][a-z]+) \((D-\d{2})\)")
        |> Seq.map (fun m -> m.Groups[2].Value, m.Groups[1].Value)
        |> Seq.distinct
        |> List.ofSeq

    let wrong =
        pairs
        |> List.filter (fun (id, name) -> seed.Drivers |> List.exists (fun d -> d.Id = id && d.Name = name) |> not)

    test <@ not pairs.IsEmpty @>
    test <@ wrong = [] @>
