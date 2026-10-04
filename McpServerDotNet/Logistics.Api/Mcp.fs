/// MCP adapter: tools and the rules resource over the pure domain, served on /mcp.
/// F# notes: tools return plain values or `task { }` (never `Async`, the SDK would send "{}");
/// optional params need [<Optional; DefaultParameterValue>] (F# `?x` / `option` stay required).
module Logistics.Api.Mcp

open System
open System.ComponentModel
open System.Runtime.InteropServices
open System.Security.Claims
open Microsoft.AspNetCore.Authorization
open ModelContextProtocol.Protocol
open ModelContextProtocol.Server
open Logistics.Api.Domain

let private orText (result: Result<'a, string>) =
    match result with
    | Ok value -> Json.serialize value
    | Error message -> message

[<McpServerToolType>]
module Tools =

    /// The anti-pattern on purpose: a 1:1 wrapper over the data, no domain knowledge.
    [<McpServerTool(Name = "get_drivers", ReadOnly = true); Description("Returns all drivers as JSON.")>]
    let getDrivers (store: Store) : string = Json.serialize store.Value.Drivers

    [<McpServerTool(Name = "list_open_orders", ReadOnly = true)>]
    [<Description("Lists open Transport Orders (not dispatched yet and not finished), earliest first. "
                  + "Pass from and/or to to keep only orders whose time window overlaps that range.")>]
    let listOpenOrders
        (store: Store)
        (clock: TimeProvider)
        ([<Description("Start of the range, ISO 8601 with offset, e.g. 2026-10-04T00:00:00Z. Omit for no lower bound.");
           Optional;
           DefaultParameterValue(null)>] from: Nullable<DateTimeOffset>)
        ([<Description("End of the range, ISO 8601 with offset, e.g. 2026-10-05T00:00:00Z. Omit for no upper bound.");
           Optional;
           DefaultParameterValue(null)>] ``to``: Nullable<DateTimeOffset>)
        : string =
        openOrders (clock.GetUtcNow()) store.Value
        |> List.filter (fun o ->
            (not from.HasValue || o.Window.To > from.Value)
            && (not ``to``.HasValue || o.Window.From < ``to``.Value))
        |> Json.serialize

    [<McpServerTool(Name = "find_dispatch_options", ReadOnly = true)>]
    [<Description("Finds who can take a Transport Order: every admissible Driver + Tractor unit + Trailer combination, "
                  + "checked against all dispatch rules (existing Dispatches and Absences, C+E licence and Driver CPC validity, "
                  + "ADR certificate for Dangerous goods, Low deck tractor for Mega trailers, trailer type). "
                  + "Also returns the reasons every other driver, tractor unit and trailer is rejected.")>]
    let findDispatchOptions
        (store: Store)
        ([<Description("Transport Order id, e.g. ORD-103.")>] orderId: string)
        : string =
        Domain.findDispatchOptions store.Value orderId
        |> Result.mapError DispatchError.describe
        |> orText

    [<McpServerTool(Name = "get_driver_schedule", ReadOnly = true)>]
    [<Description("Shows a driver's Dispatches and Absences (sick leave, holiday) that overlap the range from..to, earliest first.")>]
    let getDriverSchedule
        (store: Store)
        ([<Description("Driver id, e.g. D-01.")>] driverId: string)
        ([<Description("Start of the range, ISO 8601 with offset, e.g. 2026-10-04T00:00:00Z.")>] from: DateTimeOffset)
        ([<Description("End of the range, ISO 8601 with offset, e.g. 2026-10-05T00:00:00Z.")>] ``to``: DateTimeOffset)
        : string =
        driverSchedule store.Value driverId from ``to`` |> orText

    /// Mutating, so not ReadOnly: clients ask the user before calling it.
    /// Domain errors come back as tool text (isError = true), never as a protocol error.
    /// Needs role `dispatcher` once /mcp is protected (enforced by AddAuthorizationFilters).
    [<McpServerTool(Name = "dispatch_order", Destructive = false, Idempotent = false, OpenWorld = false)>]
    [<Authorize(Roles = Dispatcher.Role)>]
    [<Description("Dispatches a Transport Order: assigns one Driver, one Tractor unit and one Trailer for the order's whole window. "
                  + "All dispatch rules are checked; if any is broken nothing is dispatched and the reasons are returned. "
                  + "Use find_dispatch_options first to pick an admissible combination.")>]
    let dispatchOrder
        (store: Store)
        ([<Description("Transport Order id, e.g. ORD-103.")>] orderId: string)
        ([<Description("Driver id, e.g. D-01.")>] driverId: string)
        ([<Description("Tractor unit id, e.g. TRK-02.")>] tractorId: string)
        ([<Description("Trailer id, e.g. TRL-02.")>] trailerId: string)
        ([<Optional; DefaultParameterValue(null: ClaimsPrincipal)>] user: ClaimsPrincipal)
        : CallToolResult =
        let request =
            { OrderId = orderId
              DriverId = driverId
              TractorId = tractorId
              TrailerId = trailerId }

        let text, isError =
            match Store.dispatch store (Dispatcher.nameOf user) request with
            | Ok d -> describeDispatch store.Value d, false
            | Error e -> DispatchError.describe e, true

        CallToolResult(Content = ResizeArray<ContentBlock>([ TextContentBlock(Text = text) :> ContentBlock ]), IsError = isError)

[<McpServerResourceType>]
module Resources =

    let rulesMarkdown =
        """# Dispatch rules

A Dispatch assigns one Driver, one Tractor unit and one Trailer to a Transport Order for its whole time window.
It is allowed only if all five rules hold:

1. **No overlaps.** The Driver, the Tractor unit and the Trailer are not on another Dispatch whose window overlaps the order window. A Driver's Absence (sick leave, holiday) counts as busy.
2. **Valid licences.** The Driver's C+E licence and Driver CPC are valid for the whole order window.
3. **Dangerous goods need ADR.** A Dangerous goods order needs a Driver with an ADR certificate valid for the whole order window.
4. **Mega needs Low deck.** A Mega trailer can only be pulled by a Low deck Tractor unit.
5. **Trailer type matches.** The Trailer type is the one the order requires (Curtainsider, Mega or Reefer).
"""

    [<McpServerResource(UriTemplate = "logistics://rules", Name = "rules", MimeType = "text/markdown")>]
    [<Description("The 5 dispatch rules every Dispatch must satisfy.")>]
    let rules () : string = rulesMarkdown
