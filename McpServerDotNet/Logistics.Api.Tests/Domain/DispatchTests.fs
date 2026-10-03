/// `dispatch` and `cancelDispatch` on hand-built Dispatch boards. Rule details live in RuleTests.
module Logistics.Api.Tests.Domain.DispatchTests

open Logistics.Api
open Logistics.Api.Domain
open Logistics.Api.Tests.Domain.Boards
open Swensen.Unquote
open Xunit

let private request orderId driverId tractorId trailerId : DispatchRequest =
    { OrderId = orderId
      DriverId = driverId
      TractorId = tractorId
      TrailerId = trailerId }

let private ord1 = order "ORD-1" General Curtainsider

let private simple dispatches =
    board [ driver "D-1" ] [ tractor "T-1" Standard ] [ trailer "C-1" Curtainsider ] [ ord1 ] dispatches

[<Fact>]
let ``dispatch records the combination and the Dispatcher under the next Dispatch id`` () =
    let other = order "ORD-0" General Curtainsider

    let b =
        { simple [ dispatch "DSP-007" other "D-9" "T-9" "C-9" ] with
            Orders = [ other; ord1 ] }

    test
        <@
            Domain.dispatch b "alice" (request "ORD-1" "D-1" "T-1" "C-1") = Ok
                { Id = "DSP-008"
                  OrderId = "ORD-1"
                  DriverId = "D-1"
                  TractorId = "T-1"
                  TrailerId = "C-1"
                  Dispatcher = "alice" }
        @>

[<Fact>]
let ``dispatch refuses a combination breaking the rules, with every reason as readable text`` () =
    let b =
        board
            [ { driver "D-1" with AdrCertificateExpiry = None } ]
            [ tractor "T-1" Standard ]
            [ trailer "R-1" Reefer ]
            [ order "ORD-1" DangerousGoods Mega ]
            []

    let result = Domain.dispatch b "alice" (request "ORD-1" "D-1" "T-1" "R-1")

    test
        <@
            result |> Result.mapError DispatchError.describe = Error(
                "Can't dispatch ORD-1:\n"
                + "- Driver D-1 (D-1): No ADR certificate (required for Dangerous goods)\n"
                + "- Tractor unit T-1: Standard tractor unit can't pull a Mega trailer (Low deck required)\n"
                + "- Trailer R-1: Trailer type Reefer does not match required Mega"
            )
        @>

[<Fact>]
let ``dispatch refuses an unknown driver, tractor unit, trailer or order`` () =
    let describe r =
        Domain.dispatch (simple []) "alice" r |> Result.mapError DispatchError.describe

    test <@ describe (request "ORD-9" "D-1" "T-1" "C-1") = Error "Order ORD-9 not found" @>
    test <@ describe (request "ORD-1" "D-9" "T-1" "C-1") = Error "Driver D-9 not found" @>
    test <@ describe (request "ORD-1" "D-1" "T-9" "C-1") = Error "Tractor unit T-9 not found" @>
    test <@ describe (request "ORD-1" "D-1" "T-1" "C-9") = Error "Trailer C-9 not found" @>

[<Fact>]
let ``dispatch refuses an order that already has a Dispatch`` () =
    let b = simple [ dispatch "DSP-001" ord1 "D-1" "T-1" "C-1" ]

    test
        <@
            Domain.dispatch b "alice" (request "ORD-1" "D-1" "T-1" "C-1")
            |> Result.mapError DispatchError.describe = Error "Order ORD-1 is already dispatched (DSP-001)"
        @>

[<Fact>]
let ``an added Dispatch shows up in the driver's schedule and is gone after cancelling it`` () =
    let dsp = dispatch "DSP-001" ord1 "D-1" "T-1" "C-1"
    let withDispatch = addDispatch (simple []) dsp

    let schedule b =
        driverSchedule b "D-1" (at 0) (at 23) |> Result.map (List.choose _.OrderId)

    test <@ schedule withDispatch = Ok [ "ORD-1" ] @>
    test <@ cancelDispatch withDispatch "DSP-001" |> Result.map schedule = Ok(Ok []) @>
    test <@ cancelDispatch withDispatch "DSP-404" = Error "Dispatch DSP-404 not found" @>
