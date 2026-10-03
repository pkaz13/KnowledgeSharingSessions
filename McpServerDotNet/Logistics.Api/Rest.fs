/// REST adapter: thin HTTP endpoints over the pure domain. Stays open in every auth mode.
module Logistics.Api.Rest

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Logistics.Api.Domain

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

    // Domain errors as plain text: 404 for an unknown id, 409 for anything the rules refuse.
    app.MapPost(
        "/dispatches",
        Func<DispatchRequest, HttpContext, Store, IResult>(fun request ctx store ->
            match Store.dispatch store (Dispatcher.nameOf ctx.User) request with
            | Ok d -> Results.Created($"/dispatches/{d.Id}", d)
            | Error(NotFound _ as e) -> Results.Text(DispatchError.describe e, statusCode = 404)
            | Error e -> Results.Text(DispatchError.describe e, statusCode = 409))
    )
    |> ignore

    app.MapDelete(
        "/dispatches/{id}",
        Func<string, Store, IResult>(fun id store ->
            match Store.cancel store id with
            | Ok() -> Results.NoContent()
            | Error message -> Results.Text(message, statusCode = 404))
    )
    |> ignore
