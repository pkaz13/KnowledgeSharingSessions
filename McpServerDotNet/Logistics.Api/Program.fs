module Logistics.Api.Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting

/// Anchor type for WebApplicationFactory in tests.
type Marker = class end

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.AddServiceDefaults() |> ignore

    let app = builder.Build()
    app.MapDefaultEndpoints() |> ignore

    app.Run()
    0
