/// In-memory demo data, recreated on every start. All dates are relative to `now` (UTC).
///
/// Tomorrow (T) at a glance, hours UTC:
///   Existing Dispatches: ORD-099 (today 18 -> T 12) Anna, TRK-03, TRL-06
///                        ORD-100 (T 07-15) Pawel, TRK-05 Low deck, TRL-05
///                        ORD-101 (T 06-14) Marek, TRK-01, TRL-01
///                        ORD-102 (T 05-13) Katarzyna, TRK-04 Low deck, TRL-03
///   Open: ORD-103 Dangerous goods (T 08-16): only Lukasz + TRK-02 + TRL-02; Marek has ADR but is busy on ORD-101
///         ORD-104 Mega (T 09-17): both Low deck tractors are dispatched
///         ORD-105 Foodstuffs (T 14-20): overlaps ORD-103 (scene 4 forced overlap)
///   Traps for every order tomorrow: Piotr's Driver CPC expired, Tomasz on sick leave.
module Logistics.Api.Seed

open System
open Logistics.Api.Domain

let create (now: DateTimeOffset) : DispatchBoard =
    let today = DateOnly.FromDateTime(now.UtcDateTime)
    let days n = today.AddDays n

    let at (day: DateOnly) hour =
        DateTimeOffset(day.ToDateTime(TimeOnly(hour, 0)), TimeSpan.Zero)

    let tomorrow = days 1
    let window day fromHour toHour = { From = at day fromHour; To = at day toHour }

    let driver id name adr absences cpc =
        { Id = id
          Name = name
          CeLicenceExpiry = days 900
          DriverCpcExpiry = cpc
          AdrCertificateExpiry = adr
          Absences = absences }

    let drivers =
        [ driver "D-01" "Lukasz Nowak" (Some(days 300)) [] (days 400)
          driver "D-02" "Marek Kowalski" (Some(days 200)) [] (days 500)
          driver "D-03" "Anna Wisniewska" None [] (days 300)
          driver "D-04" "Piotr Zielinski" None [] (days -22)
          driver
              "D-05"
              "Tomasz Wojcik"
              None
              [ { Kind = SickLeave
                  From = tomorrow
                  To = days 3 } ]
              (days 250)
          driver "D-06" "Katarzyna Lewandowska" None [] (days 600)
          driver
              "D-07"
              "Pawel Kaminski"
              None
              [ { Kind = Holiday
                  From = days 10
                  To = days 17 } ]
              (days 350)
          driver "D-08" "Jan Dabrowski" (Some(days -40)) [] (days 450) ]

    let tractors =
        [ { Id = "TRK-01"; Registration = "PO 1K234"; Kind = Standard }
          { Id = "TRK-02"; Registration = "PO 2M561"; Kind = Standard }
          { Id = "TRK-03"; Registration = "PO 3N802"; Kind = Standard }
          { Id = "TRK-04"; Registration = "PO 4L117"; Kind = LowDeck }
          { Id = "TRK-05"; Registration = "PO 5L349"; Kind = LowDeck } ]

    let trailers =
        [ { Id = "TRL-01"; Registration = "PO 7C101"; Type = Curtainsider }
          { Id = "TRL-02"; Registration = "PO 7C102"; Type = Curtainsider }
          { Id = "TRL-03"; Registration = "PO 7M201"; Type = Mega }
          { Id = "TRL-04"; Registration = "PO 7M202"; Type = Mega }
          { Id = "TRL-05"; Registration = "PO 7R301"; Type = Reefer }
          { Id = "TRL-06"; Registration = "PO 7R302"; Type = Reefer } ]

    let order id origin destination cargo trailer window =
        { Id = id
          Origin = origin
          Destination = destination
          Cargo = cargo
          RequiredTrailer = trailer
          Window = window }

    let orders =
        [ order "ORD-099" "Gdansk" "Hamburg" Foodstuffs Reefer { From = at today 18; To = at tomorrow 12 }
          order "ORD-100" "Poznan" "Wroclaw" Foodstuffs Reefer (window tomorrow 7 15)
          order "ORD-101" "Poznan" "Leipzig" General Curtainsider (window tomorrow 6 14)
          order "ORD-102" "Poznan" "Prague" General Mega (window tomorrow 5 13)
          order "ORD-103" "Poznan" "Berlin" DangerousGoods Curtainsider (window tomorrow 8 16)
          order "ORD-104" "Poznan" "Dresden" General Mega (window tomorrow 9 17)
          order "ORD-105" "Poznan" "Szczecin" Foodstuffs Reefer (window tomorrow 14 20)
          order "ORD-106" "Poznan" "Warsaw" General Curtainsider (window (days 2) 7 15)
          order "ORD-107" "Poznan" "Lodz" Foodstuffs Reefer (window (days 2) 6 12)
          order "ORD-108" "Poznan" "Hannover" DangerousGoods Curtainsider (window (days 3) 8 18) ]

    let dispatch id orderId driverId tractorId trailerId =
        { Id = id
          OrderId = orderId
          DriverId = driverId
          TractorId = tractorId
          TrailerId = trailerId
          Dispatcher = "alice" }

    let dispatches =
        [ dispatch "DSP-001" "ORD-099" "D-03" "TRK-03" "TRL-06"
          dispatch "DSP-002" "ORD-100" "D-07" "TRK-05" "TRL-05"
          dispatch "DSP-003" "ORD-101" "D-02" "TRK-01" "TRL-01"
          dispatch "DSP-004" "ORD-102" "D-06" "TRK-04" "TRL-03" ]

    { Drivers = drivers
      Tractors = tractors
      Trailers = trailers
      Orders = orders
      Dispatches = dispatches }
