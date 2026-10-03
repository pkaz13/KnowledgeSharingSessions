namespace Logistics.Api

open Logistics.Api.Domain

/// The in-memory store shared by the REST and MCP adapters: a mutable reference to the immutable Dispatch board.
type Store = DispatchBoard ref
