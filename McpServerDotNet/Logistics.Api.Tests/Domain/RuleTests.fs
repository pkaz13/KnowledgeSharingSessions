/// The 5 rules, each on a small hand-built Dispatch board. Rules are tested only here.
module Logistics.Api.Tests.Domain.RuleTests

open System
open Logistics.Api.Domain
open Logistics.Api.Tests.Domain.Boards
open Swensen.Unquote
open Xunit

[<Fact>]
let ``Rule 1: a driver, tractor unit and trailer already dispatched in an overlapping window are rejected`` () =
    let earlier =
        { order "ORD-1" General Curtainsider with
            Window = { From = at 6; To = at 10 } }

    let later = order "ORD-2" General Curtainsider

    let b =
        board
            [ driver "D-1"; driver "D-2" ]
            [ tractor "T-1" Standard; tractor "T-2" Standard ]
            [ trailer "C-1" Curtainsider; trailer "C-2" Curtainsider ]
            [ earlier; later ]
            [ dispatch "DSP-1" earlier "D-1" "T-1" "C-1" ]

    let result = optionsFor b "ORD-2"

    let busy =
        [ "Already dispatched on ORD-1 (2026-10-04 06:00 to 2026-10-04 10:00 UTC)" ]

    test <@ result.Options = [ option "D-2" "T-2" "C-2" ] @>
    test <@ result.RejectedDrivers = [ { Id = "D-1"; Reasons = busy } ] @>
    test <@ result.RejectedTractors = [ { Id = "T-1"; Reasons = busy } ] @>
    test <@ result.RejectedTrailers = [ { Id = "C-1"; Reasons = busy } ] @>

[<Fact>]
let ``Rule 1: a dispatch ending when the order starts is not an overlap`` () =
    let earlier =
        { order "ORD-1" General Curtainsider with
            Window = { From = at 4; To = at 8 } }

    let b =
        board
            [ driver "D-1" ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider ]
            [ earlier; order "ORD-2" General Curtainsider ]
            [ dispatch "DSP-1" earlier "D-1" "T-1" "C-1" ]

    test <@ (optionsFor b "ORD-2").Options = [ option "D-1" "T-1" "C-1" ] @>

[<Fact>]
let ``Rule 1: an Absence counts as busy`` () =
    let sick =
        { driver "D-1" with
            Absences =
                [ { Kind = SickLeave
                    From = DateOnly(2026, 10, 4)
                    To = DateOnly(2026, 10, 4) } ] }

    let onHoliday =
        { driver "D-2" with
            Absences =
                [ { Kind = Holiday
                    From = DateOnly(2026, 10, 1)
                    To = DateOnly(2026, 10, 7) } ] }

    let b =
        board
            [ sick; onHoliday; driver "D-3" ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider ]
            [ order "ORD-1" General Curtainsider ]
            []

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [ option "D-3" "T-1" "C-1" ] @>

    test
        <@
            result.RejectedDrivers = [ { Id = "D-1"; Reasons = [ "On sick leave 2026-10-04" ] }
                                       { Id = "D-2"; Reasons = [ "On holiday 2026-10-01 to 2026-10-07" ] } ]
        @>

[<Fact>]
let ``Rule 2: C+E licence and Driver CPC must be valid during the order window`` () =
    let cpcExpired =
        { driver "D-1" with
            DriverCpcExpiry = DateOnly(2026, 9, 12) }

    let licenceExpired =
        { driver "D-2" with
            CeLicenceExpiry = DateOnly(2026, 10, 3) }

    let b =
        board
            [ cpcExpired; licenceExpired; driver "D-3" ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider ]
            [ order "ORD-1" General Curtainsider ]
            []

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [ option "D-3" "T-1" "C-1" ] @>

    test
        <@
            result.RejectedDrivers = [ { Id = "D-1"; Reasons = [ "Driver CPC expired 2026-09-12" ] }
                                       { Id = "D-2"; Reasons = [ "C+E licence expired 2026-10-03" ] } ]
        @>

[<Fact>]
let ``Rule 2: a document expiring before a multi-day order ends is rejected`` () =
    let longOrder =
        { order "ORD-1" General Curtainsider with
            Window = { From = at 8; To = at 8 + TimeSpan.FromDays 2.0 } }

    let b =
        board
            [ { driver "D-1" with
                  DriverCpcExpiry = DateOnly(2026, 10, 5) } ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider ]
            [ longOrder ]
            []

    test
        <@
            (optionsFor b "ORD-1").RejectedDrivers.Head = { Id = "D-1"
                                                            Reasons = [ "Driver CPC expires 2026-10-05, before the order ends" ] }
        @>

[<Fact>]
let ``Rule 3: Dangerous goods require a valid ADR certificate`` () =
    let b =
        board
            [ { driver "D-1" with
                  AdrCertificateExpiry = Some(DateOnly(2027, 5, 1)) }
              driver "D-2"
              { driver "D-3" with
                  AdrCertificateExpiry = Some(DateOnly(2026, 8, 31)) } ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider ]
            [ order "ORD-1" DangerousGoods Curtainsider ]
            []

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [ option "D-1" "T-1" "C-1" ] @>

    test
        <@
            result.RejectedDrivers = [ { Id = "D-2"; Reasons = [ "No ADR certificate (required for Dangerous goods)" ] }
                                       { Id = "D-3"; Reasons = [ "ADR certificate expired 2026-08-31" ] } ]
        @>

[<Fact>]
let ``Rule 4: a Mega trailer requires a Low deck tractor unit`` () =
    let b =
        board
            [ driver "D-1" ]
            [ tractor "T-1" Standard; tractor "T-2" LowDeck ]
            [ trailer "M-1" Mega ]
            [ order "ORD-1" General Mega ]
            []

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [ option "D-1" "T-2" "M-1" ] @>

    test
        <@
            result.RejectedTractors = [ { Id = "T-1"
                                          Reasons = [ "Standard tractor unit can't pull a Mega trailer (Low deck required)" ] } ]
        @>

[<Fact>]
let ``Rule 4: with no free Low deck tractor unit an otherwise eligible driver gets no option`` () =
    let other =
        { order "ORD-0" General Mega with
            Window = { From = at 6; To = at 12 } }

    let b =
        board
            [ driver "D-1"; driver "D-2" ]
            [ tractor "T-1" Standard; tractor "T-2" LowDeck ]
            [ trailer "M-1" Mega; trailer "M-2" Mega ]
            [ other; order "ORD-1" General Mega ]
            [ dispatch "DSP-1" other "D-2" "T-2" "M-2" ]

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [] @>

    test
        <@
            result.RejectedDrivers.Head = { Id = "D-1"
                                            Reasons = [ "No free tractor unit and trailer combination for this order" ] }
        @>

[<Fact>]
let ``Rule 5: the trailer type must match the order`` () =
    let b =
        board
            [ driver "D-1" ]
            [ tractor "T-1" Standard ]
            [ trailer "C-1" Curtainsider; trailer "R-1" Reefer ]
            [ order "ORD-1" Foodstuffs Reefer ]
            []

    let result = optionsFor b "ORD-1"

    test <@ result.Options = [ option "D-1" "T-1" "R-1" ] @>

    test
        <@
            result.RejectedTrailers = [ { Id = "C-1"
                                          Reasons = [ "Trailer type Curtainsider does not match required Reefer" ] } ]
        @>
