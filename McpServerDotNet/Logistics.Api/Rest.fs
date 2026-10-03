/// REST adapter: thin HTTP endpoints over the pure domain. Stays open in every auth mode.
module Logistics.Api.Rest

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Logistics.Api.Domain

/// The in-memory store: a mutable reference to the immutable Dispatch board.
type Store = DispatchBoard ref

let private orNotFound (value: 'a option) message =
    match value with
    | Some v -> Results.Ok v
    | None -> Results.NotFound(message: string)

let mapEndpoints (app: IEndpointRouteBuilder) =
    app.MapGet("/drivers", Func<Store, IResult>(fun store -> Results.Ok store.Value.Drivers))
    |> ignore

    app.MapGet(
        "/drivers/{id}",
        Func<string, Store, IResult>(fun id store ->
            orNotFound (tryFindDriver store.Value id) $"Driver {id} not found")
    )
    |> ignore

    app.MapGet("/tractors", Func<Store, IResult>(fun store -> Results.Ok store.Value.Tractors))
    |> ignore

    app.MapGet("/trailers", Func<Store, IResult>(fun store -> Results.Ok store.Value.Trailers))
    |> ignore

    app.MapGet(
        "/orders",
        Func<HttpContext, Store, TimeProvider, IResult>(fun ctx store clock ->
            match ctx.Request.Query["status"].ToString() with
            | "" -> Results.Ok store.Value.Orders
            | "open" -> Results.Ok(openOrders (clock.GetUtcNow()) store.Value)
            | other -> Results.BadRequest($"Unknown status '{other}'. Supported: open"))
    )
    |> ignore

    app.MapGet(
        "/orders/{id}",
        Func<string, Store, IResult>(fun id store ->
            orNotFound (tryFindOrder store.Value id) $"Order {id} not found")
    )
    |> ignore

    app.MapGet(
        "/orders/{id}/dispatch-options",
        Func<string, Store, IResult>(fun id store ->
            match tryFindOrder store.Value id, findDispatchOptions store.Value id with
            | None, _ -> Results.NotFound $"Order {id} not found"
            | _, Ok options -> Results.Ok options
            | _, Error message -> Results.Conflict message)
    )
    |> ignore

    app.MapGet("/dispatches", Func<Store, IResult>(fun store -> Results.Ok store.Value.Dispatches))
    |> ignore
