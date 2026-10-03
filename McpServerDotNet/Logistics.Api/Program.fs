module Logistics.Api.Program

open System
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open Microsoft.Extensions.Hosting
open Logistics.Api

/// Anchor type for WebApplicationFactory in tests.
type Marker = class end

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.AddServiceDefaults() |> ignore

    builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System)

    // Reseeded on every start, dates relative to startup.
    builder.Services.AddSingleton<Rest.Store>(fun sp ->
        ref (Seed.create (sp.GetRequiredService<TimeProvider>().GetUtcNow())))
    |> ignore

    builder.Services.Configure<JsonOptions>(fun (o: JsonOptions) ->
        o.SerializerOptions.Converters.Add(
            JsonFSharpConverter(JsonFSharpOptions.Default().WithUnionUnwrapFieldlessTags())
        ))
    |> ignore

    let app = builder.Build()
    app.Services.GetRequiredService<Rest.Store>() |> ignore
    app.MapDefaultEndpoints() |> ignore
    Rest.mapEndpoints app

    app.Run()
    0
