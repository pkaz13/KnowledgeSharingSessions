/// Demo-scene tests on the seed (scene numbers from the demo prompt scenarios, #20).
module Logistics.Api.Tests.DemoScenes.SeedTests

open System
open Logistics.Api
open Logistics.Api.Domain
open Swensen.Unquote
open Xunit

/// Late evening, so "tomorrow" is a different date in UTC and in Europe.
let now = DateTimeOffset(2026, 10, 3, 21, 30, 0, TimeSpan.Zero)
let tomorrow = DateOnly(2026, 10, 4)
let seed = Seed.create now

let private options orderId =
    match findDispatchOptions seed orderId with
    | Ok o -> o
    | Error e -> failwith (DispatchError.describe e)

let private reasonsOf id (rejections: Rejection list) =
    rejections |> List.filter (fun r -> r.Id = id) |> List.collect _.Reasons

let private isTomorrow (o: TransportOrder) =
    DateOnly.FromDateTime(o.Window.From.UtcDateTime) = tomorrow

[<Fact>]
let ``Seed has the agreed composition`` () =
    test <@ seed.Drivers.Length = 8 @>
    test <@ seed.Tractors |> List.countBy _.Kind |> List.sort = [ Standard, 3; LowDeck, 2 ] @>
    test <@ seed.Trailers |> List.countBy _.Type |> List.sort = [ Curtainsider, 2; Mega, 2; Reefer, 2 ] @>
    test <@ (openOrders now seed).Length = 6 @>
    test <@ seed.Orders |> List.forall (fun o -> o.Cargo <> Foodstuffs || o.RequiredTrailer = Reefer) @>

[<Fact>]
let ``Seed requirement 1: at least one Dispatch already exists`` () =
    test <@ not seed.Dispatches.IsEmpty @>

[<Fact>]
let ``Scene 3: tomorrow's open orders are ORD-103, ORD-104 and ORD-105`` () =
    test <@ openOrders now seed |> List.filter isTomorrow |> List.map _.Id = [ "ORD-103"; "ORD-104"; "ORD-105" ] @>

[<Fact>]
let ``Scene 3: ORD-103 is Dangerous goods tomorrow with one option plus an ADR rejection reason`` () =
    let order = (tryFindOrder seed "ORD-103").Value
    let result = options "ORD-103"

    test <@ order.Cargo = DangerousGoods && isTomorrow order @>

    test
        <@
            result.Options = [ { DriverId = "D-01"
                                 DriverName = "Lukasz Nowak"
                                 TractorId = "TRK-02"
                                 TrailerId = "TRL-02" } ]
        @>

    test
        <@
            result.RejectedDrivers
            |> List.exists (fun r -> r.Reasons |> List.contains "No ADR certificate (required for Dangerous goods)")
        @>

[<Fact>]
let ``Scene 3: the fact that rules out the second ADR driver lies outside Driver data`` () =
    // Marek looks fine from get_drivers alone: valid C+E, Driver CPC and ADR, no Absence that day.
    let marek = (tryFindDriver seed "D-02").Value
    let order = (tryFindOrder seed "ORD-103").Value
    let window = order.Window
    let validOn (d: DateOnly) = d >= DateOnly.FromDateTime(window.To.UtcDateTime)

    test <@ validOn marek.CeLicenceExpiry && validOn marek.DriverCpcExpiry @>
    test <@ marek.AdrCertificateExpiry |> Option.exists validOn @>
    test <@ marek.Absences |> List.forall (fun a -> a.To < tomorrow || a.From > tomorrow) @>

    // ...but an existing Dispatch makes him busy.
    test <@ reasonsOf "D-02" (options "ORD-103").RejectedDrivers = [ "Already dispatched on ORD-101 (2026-10-04 06:00 to 2026-10-04 14:00 UTC)" ] @>

[<Fact>]
let ``Scene 4: ORD-105 overlaps ORD-103 and has no dispatch in the seed`` () =
    let w103 = (tryFindOrder seed "ORD-103").Value.Window
    let w105 = (tryFindOrder seed "ORD-105").Value.Window

    test <@ w105.From < w103.To && w103.From < w105.To @>
    test <@ seed.Dispatches |> List.forall (fun d -> d.OrderId <> "ORD-103" && d.OrderId <> "ORD-105") @>

[<Fact>]
let ``Scene 5: tomorrow's orders show an expired Driver CPC, a sick leave and a Mega order with no free Low deck`` () =
    let tomorrowOptions =
        openOrders now seed |> List.filter isTomorrow |> List.map (fun o -> options o.Id)

    let allDriverReasons =
        tomorrowOptions |> List.collect _.RejectedDrivers |> List.collect _.Reasons

    let mega = options "ORD-104"

    test <@ allDriverReasons |> List.exists (fun r -> r.StartsWith "Driver CPC expired") @>
    test <@ allDriverReasons |> List.contains "On sick leave 2026-10-04 to 2026-10-06" @>
    test <@ (tryFindOrder seed "ORD-104").Value.RequiredTrailer = Mega @>
    test <@ mega.Options = [] @>

    test
        <@
            seed.Tractors
            |> List.filter (fun t -> t.Kind = LowDeck)
            |> List.forall (fun t -> reasonsOf t.Id mega.RejectedTractors |> List.exists _.StartsWith("Already dispatched"))
        @>

[<Fact>]
let ``Seed dates are relative to now`` () =
    let later = Seed.create (now.AddDays 30.0)

    test <@ (tryFindOrder later "ORD-103").Value.Window.From = (tryFindOrder seed "ORD-103").Value.Window.From.AddDays 30.0 @>
