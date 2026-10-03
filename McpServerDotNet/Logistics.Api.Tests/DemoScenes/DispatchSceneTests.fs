/// Demo-scene tests for the write side (scenes 4 to 6 from #20) on the seed.
module Logistics.Api.Tests.DemoScenes.DispatchSceneTests

open System
open Logistics.Api
open Logistics.Api.Domain
open Swensen.Unquote
open Xunit

/// Same instant as SeedTests: tomorrow is 2026-10-04.
let now = DateTimeOffset(2026, 10, 3, 21, 30, 0, TimeSpan.Zero)

let private request orderId driverId tractorId trailerId : DispatchRequest =
    { OrderId = orderId
      DriverId = driverId
      TractorId = tractorId
      TrailerId = trailerId }

let private tryDispatch board r =
    dispatch board "anonymous" r |> Result.mapError DispatchError.describe

/// Scene 4: ORD-103 dispatched with its only option.
let private ord103 = request "ORD-103" "D-01" "TRK-02" "TRL-02"

let private afterScene4 () =
    let seed = Seed.create now

    match dispatch seed "anonymous" ord103 with
    | Ok d -> addDispatch seed d
    | Error e -> failwith (DispatchError.describe e)

[<Fact>]
let ``Scene 4: ORD-103 is dispatched by anonymous with its only option`` () =
    test
        <@
            tryDispatch (Seed.create now) ord103 = Ok
                { Id = "DSP-005"
                  OrderId = "ORD-103"
                  DriverId = "D-01"
                  TractorId = "TRK-02"
                  TrailerId = "TRL-02"
                  Dispatcher = "anonymous" }
        @>

[<Fact>]
let ``Scene 4: forcing the same driver and tractor onto overlapping ORD-105 fails with readable reasons`` () =
    test
        <@
            tryDispatch (afterScene4 ()) (request "ORD-105" "D-01" "TRK-02" "TRL-06") = Error(
                "Can't dispatch ORD-105:\n"
                + "- Driver D-01 (Lukasz Nowak): Already dispatched on ORD-103 (2026-10-04 08:00 to 2026-10-04 16:00 UTC)\n"
                + "- Tractor unit TRK-02: Already dispatched on ORD-103 (2026-10-04 08:00 to 2026-10-04 16:00 UTC)"
            )
        @>

[<Fact>]
let ``Scene 4: the driver's schedule tomorrow shows the new Dispatch`` () =
    let schedule =
        driverSchedule (afterScene4 ()) "D-01" (now.AddHours 2.5) (now.AddHours 26.5)
        |> Result.map (List.choose _.OrderId)

    test <@ schedule = Ok [ "ORD-103" ] @>

[<Fact>]
let ``Scene 5: planning tomorrow runs into the expired Driver CPC, the sick leave and the Mega order without a free Low deck`` () =
    let board = afterScene4 ()

    test
        <@
            tryDispatch board (request "ORD-105" "D-04" "TRK-03" "TRL-06") = Error
                "Can't dispatch ORD-105:\n- Driver D-04 (Piotr Zielinski): Driver CPC expired 2026-09-11"
        @>

    test
        <@
            tryDispatch board (request "ORD-105" "D-05" "TRK-03" "TRL-06") = Error
                "Can't dispatch ORD-105:\n- Driver D-05 (Tomasz Wojcik): On sick leave 2026-10-04 to 2026-10-06"
        @>

    test
        <@
            tryDispatch board (request "ORD-104" "D-08" "TRK-05" "TRL-04") = Error
                "Can't dispatch ORD-104:\n- Tractor unit TRK-05: Already dispatched on ORD-100 (2026-10-04 07:00 to 2026-10-04 15:00 UTC)"
        @>

[<Fact>]
let ``Scene 6: after the reseed ORD-103 can be dispatched again, now by alice`` () =
    test <@ afterScene4 () |> fun b -> tryDispatch b ord103 |> Result.isError @>

    let reseeded = Seed.create now

    test <@ dispatch reseeded "alice" ord103 |> Result.map _.Dispatcher = Ok "alice" @>
