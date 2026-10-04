namespace Logistics.Api

open Logistics.Api.Domain

/// The in-memory store shared by the REST and MCP adapters: a mutable reference to the immutable Dispatch board.
type Store = DispatchBoard ref

module Store =
    /// Runs one read-modify-write on the board atomically. `change` returns the new board
    /// (or None to keep the old one) and a value for the caller.
    let update (store: Store) (change: DispatchBoard -> DispatchBoard option * 'a) : 'a =
        lock store (fun () ->
            let board, result = change store.Value
            board |> Option.iter (fun b -> store.Value <- b)
            result)

    /// Dispatches on behalf of `dispatcher` and records the Dispatch on success.
    let dispatch (store: Store) dispatcher (request: DispatchRequest) =
        update store (fun board ->
            match Domain.dispatch board dispatcher request with
            | Ok d -> Some(addDispatch board d), Ok d
            | Error e -> None, Error e)

    /// Cancels a Dispatch; its order becomes open again.
    let cancel (store: Store) dispatchId =
        update store (fun board ->
            match cancelDispatch board dispatchId with
            | Ok b -> Some b, Ok()
            | Error e -> None, Error e)
