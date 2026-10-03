/// openOrders, driverSchedule and findDispatchOptions edge cases.
module Logistics.Api.Tests.Domain.QueryTests

open System
open Logistics.Api.Domain
open Logistics.Api.Tests.Domain.Boards
open Swensen.Unquote
open Xunit

let private ended =
    { order "ORD-1" General Curtainsider with
        Window = { From = at 1; To = at 5 } }

let private dispatched = order "ORD-2" General Curtainsider

let private late =
    { order "ORD-3" General Curtainsider with
        Window = { From = at 12; To = at 20 } }

let private early = order "ORD-4" General Curtainsider

let private sampleBoard =
    board
        [ { driver "D-1" with
              Absences =
                  [ { Kind = Holiday
                      From = DateOnly(2026, 10, 5)
                      To = DateOnly(2026, 10, 6) } ] } ]
        [ tractor "T-1" Standard ]
        [ trailer "C-1" Curtainsider ]
        [ ended; dispatched; late; early ]
        [ dispatch "DSP-1" dispatched "D-1" "T-1" "C-1" ]

[<Fact>]
let ``openOrders lists undispatched orders that haven't ended, earliest first`` () =
    test <@ openOrders (at 6) sampleBoard |> List.map _.Id = [ "ORD-4"; "ORD-3" ] @>

[<Fact>]
let ``driverSchedule lists Dispatches and Absences in the range`` () =
    let schedule =
        driverSchedule sampleBoard "D-1" (at 0) (at 0 + TimeSpan.FromDays 3.0)

    test
        <@
            schedule = Ok
                [ { From = at 8
                    To = at 16
                    Activity = "Dispatch DSP-1: ORD-2 Poznan -> Berlin, tractor T-1, trailer C-1"
                    OrderId = Some "ORD-2" }
                  { From = DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero)
                    To = DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero)
                    Activity = "On holiday 2026-10-05 to 2026-10-06"
                    OrderId = None } ]
        @>

[<Fact>]
let ``driverSchedule leaves out entries outside the range`` () =
    test <@ driverSchedule sampleBoard "D-1" (at 16) (at 23) = Ok [] @>

[<Fact>]
let ``driverSchedule for an unknown driver is an error`` () =
    test <@ driverSchedule sampleBoard "D-9" (at 0) (at 23) = Error "Driver D-9 not found" @>

[<Fact>]
let ``findDispatchOptions for an unknown order is an error`` () =
    test <@ findDispatchOptions sampleBoard "ORD-9" = Error(NotFound "Order ORD-9 not found") @>

[<Fact>]
let ``findDispatchOptions for a dispatched order is an error`` () =
    test <@ findDispatchOptions sampleBoard "ORD-2" = Error(AlreadyDispatched("ORD-2", "DSP-1")) @>
