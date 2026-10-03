/// Pure logistics domain over an immutable Dispatch board. No I/O; `now` is passed in.
module Logistics.Api.Domain

open System

type AbsenceKind =
    | SickLeave
    | Holiday

/// Whole days, both ends inclusive.
type Absence =
    { Kind: AbsenceKind
      From: DateOnly
      To: DateOnly }

type Driver =
    { Id: string
      Name: string
      CeLicenceExpiry: DateOnly
      DriverCpcExpiry: DateOnly
      AdrCertificateExpiry: DateOnly option
      Absences: Absence list }

type TractorKind =
    | Standard
    | LowDeck

type TractorUnit =
    { Id: string
      Registration: string
      Kind: TractorKind }

type TrailerType =
    | Curtainsider
    | Mega
    | Reefer

type Trailer =
    { Id: string
      Registration: string
      Type: TrailerType }

type CargoType =
    | General
    | Foodstuffs
    | DangerousGoods

/// Half-open time window [From, To).
type Window =
    { From: DateTimeOffset
      To: DateTimeOffset }

type TransportOrder =
    { Id: string
      Origin: string
      Destination: string
      Cargo: CargoType
      RequiredTrailer: TrailerType
      Window: Window }

type Dispatch =
    { Id: string
      OrderId: string
      DriverId: string
      TractorId: string
      TrailerId: string
      Dispatcher: string }

type DispatchBoard =
    { Drivers: Driver list
      Tractors: TractorUnit list
      Trailers: Trailer list
      Orders: TransportOrder list
      Dispatches: Dispatch list }

/// One admissible Driver + Tractor unit + Trailer combination for an order.
type DispatchOption =
    { DriverId: string
      DriverName: string
      TractorId: string
      TrailerId: string }

/// Why a Driver, Tractor unit or Trailer can't take the order. Reasons are readable text.
type Rejection = { Id: string; Reasons: string list }

type DispatchOptions =
    { OrderId: string
      Options: DispatchOption list
      RejectedDrivers: Rejection list
      RejectedTractors: Rejection list
      RejectedTrailers: Rejection list }

type ScheduleEntry =
    { From: DateTimeOffset
      To: DateTimeOffset
      Activity: string
      OrderId: string option }

// ---- helpers ----

let private overlaps (a: Window) (b: Window) = a.From < b.To && b.From < a.To

let private date (d: DateOnly) = d.ToString("yyyy-MM-dd")

let private stamp (t: DateTimeOffset) =
    t.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")

let private describeWindow (w: Window) =
    $"{stamp w.From} to {stamp w.To} UTC"

let private dayStart (d: DateOnly) =
    DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)

let private absenceWindow (a: Absence) =
    { From = dayStart a.From
      To = dayStart (a.To.AddDays 1) }

let private describeAbsence (a: Absence) =
    let kind =
        match a.Kind with
        | SickLeave -> "sick leave"
        | Holiday -> "holiday"

    if a.From = a.To then
        $"On {kind} {date a.From}"
    else
        $"On {kind} {date a.From} to {date a.To}"

let private trailerTypeName =
    function
    | Curtainsider -> "Curtainsider"
    | Mega -> "Mega"
    | Reefer -> "Reefer"

let private orderOf (board: DispatchBoard) orderId =
    board.Orders |> List.tryFind (fun o -> o.Id = orderId)

/// Rule 1 for Dispatches: busy reasons for every Dispatch of the resource overlapping the window.
let private busyReasons board (window: Window) (usesResource: Dispatch -> bool) =
    board.Dispatches
    |> List.filter usesResource
    |> List.choose (fun d ->
        orderOf board d.OrderId
        |> Option.filter (fun o -> overlaps o.Window window)
        |> Option.map (fun o -> $"Already dispatched on {o.Id} ({describeWindow o.Window})"))

/// Rule 2: a document must stay valid until the end of the order window.
let private validity (what: string) (expiry: DateOnly) (window: Window) =
    let fromDay = DateOnly.FromDateTime(window.From.UtcDateTime)
    let toDay = DateOnly.FromDateTime(window.To.UtcDateTime)

    if expiry < fromDay then Some $"{what} expired {date expiry}"
    elif expiry < toDay then Some $"{what} expires {date expiry}, before the order ends"
    else None

let private driverReasons board (order: TransportOrder) (driver: Driver) =
    [ yield! validity "C+E licence" driver.CeLicenceExpiry order.Window |> Option.toList
      yield! validity "Driver CPC" driver.DriverCpcExpiry order.Window |> Option.toList
      if order.Cargo = DangerousGoods then
          match driver.AdrCertificateExpiry with
          | None -> yield "No ADR certificate (required for Dangerous goods)"
          | Some expiry -> yield! validity "ADR certificate" expiry order.Window |> Option.toList
      yield!
          driver.Absences
          |> List.filter (fun a -> overlaps (absenceWindow a) order.Window)
          |> List.map describeAbsence
      yield! busyReasons board order.Window (fun d -> d.DriverId = driver.Id) ]

let private tractorReasons board (order: TransportOrder) (tractor: TractorUnit) =
    [ if order.RequiredTrailer = Mega && tractor.Kind = Standard then
          yield "Standard tractor unit can't pull a Mega trailer (Low deck required)"
      yield! busyReasons board order.Window (fun d -> d.TractorId = tractor.Id) ]

let private trailerReasons board (order: TransportOrder) (trailer: Trailer) =
    [ if trailer.Type <> order.RequiredTrailer then
          yield
              $"Trailer type {trailerTypeName trailer.Type} does not match required {trailerTypeName order.RequiredTrailer}"
      yield! busyReasons board order.Window (fun d -> d.TrailerId = trailer.Id) ]

let private partition reasonsOf (idOf: 'a -> string) (items: 'a list) =
    let checkedItems = items |> List.map (fun x -> x, reasonsOf x)
    let admissible = checkedItems |> List.filter (snd >> List.isEmpty) |> List.map fst

    let rejected =
        checkedItems
        |> List.filter (snd >> List.isEmpty >> not)
        |> List.map (fun (x, reasons) -> { Id = idOf x; Reasons = reasons })

    admissible, rejected

// ---- public functions ----

let tryFindOrder (board: DispatchBoard) orderId = orderOf board orderId

let tryFindDriver (board: DispatchBoard) driverId =
    board.Drivers |> List.tryFind (fun d -> d.Id = driverId)

/// Transport Orders without a Dispatch whose window hasn't ended yet, earliest first.
let openOrders (now: DateTimeOffset) (board: DispatchBoard) =
    let dispatched = board.Dispatches |> List.map _.OrderId |> Set.ofList

    board.Orders
    |> List.filter (fun o -> not (dispatched.Contains o.Id) && o.Window.To > now)
    |> List.sortBy _.Window.From

/// Admissible Driver + Tractor unit + Trailer combinations for an order, plus the
/// reasons every other Driver, Tractor unit and Trailer is rejected.
let findDispatchOptions (board: DispatchBoard) (orderId: string) : Result<DispatchOptions, string> =
    match orderOf board orderId with
    | None -> Error $"Order {orderId} not found"
    | Some order ->
        match board.Dispatches |> List.tryFind (fun d -> d.OrderId = orderId) with
        | Some existing -> Error $"Order {orderId} is already dispatched ({existing.Id})"
        | None ->
            let drivers, rejectedDrivers =
                partition (driverReasons board order) (fun (d: Driver) -> d.Id) board.Drivers

            let tractors, rejectedTractors =
                partition (tractorReasons board order) (fun (t: TractorUnit) -> t.Id) board.Tractors

            let trailers, rejectedTrailers =
                partition (trailerReasons board order) (fun (t: Trailer) -> t.Id) board.Trailers

            let options =
                [ for d in drivers do
                      for t in tractors do
                          for tr in trailers ->
                              { DriverId = d.Id
                                DriverName = d.Name
                                TractorId = t.Id
                                TrailerId = tr.Id } ]

            let idleDrivers =
                if options.IsEmpty then
                    drivers
                    |> List.map (fun d ->
                        { Id = d.Id
                          Reasons = [ "No free tractor unit and trailer combination for this order" ] })
                else
                    []

            Ok
                { OrderId = order.Id
                  Options = options
                  RejectedDrivers = idleDrivers @ rejectedDrivers
                  RejectedTractors = rejectedTractors
                  RejectedTrailers = rejectedTrailers }

/// A Driver's Dispatches and Absences overlapping [from, to), earliest first.
let driverSchedule (board: DispatchBoard) (driverId: string) (from: DateTimeOffset) (``to``: DateTimeOffset) =
    match tryFindDriver board driverId with
    | None -> Error $"Driver {driverId} not found"
    | Some driver ->
        let range = { From = from; To = ``to`` }

        let dispatches =
            board.Dispatches
            |> List.filter (fun d -> d.DriverId = driverId)
            |> List.choose (fun d ->
                orderOf board d.OrderId
                |> Option.filter (fun o -> overlaps o.Window range)
                |> Option.map (fun o ->
                    { From = o.Window.From
                      To = o.Window.To
                      Activity =
                        $"Dispatch {d.Id}: {o.Id} {o.Origin} -> {o.Destination}, tractor {d.TractorId}, trailer {d.TrailerId}"
                      OrderId = Some o.Id }))

        let absences =
            driver.Absences
            |> List.filter (fun a -> overlaps (absenceWindow a) range)
            |> List.map (fun a ->
                let w = absenceWindow a

                { From = w.From
                  To = w.To
                  Activity = describeAbsence a
                  OrderId = None })

        Ok(dispatches @ absences |> List.sortBy _.From)

/// What a Dispatcher asks for: one Driver, Tractor unit and Trailer for one Transport Order.
type DispatchRequest =
    { OrderId: string
      DriverId: string
      TractorId: string
      TrailerId: string }

/// Why a Dispatch was refused. `DispatchError.describe` turns it into readable text.
type DispatchError =
    | NotFound of string
    | AlreadyDispatched of orderId: string * dispatchId: string
    | RulesBroken of orderId: string * reasons: string list

module DispatchError =
    let describe =
        function
        | NotFound message -> message
        | AlreadyDispatched(orderId, dispatchId) -> $"Order {orderId} is already dispatched ({dispatchId})"
        | RulesBroken(orderId, reasons) ->
            let lines = reasons |> List.map (fun r -> $"- {r}") |> String.concat "\n"
            $"Can't dispatch {orderId}:\n{lines}"

let private nextDispatchId (board: DispatchBoard) =
    let number (id: string) =
        match Int32.TryParse(id.Replace("DSP-", "")) with
        | true, n -> n
        | _ -> 0

    let next = (board.Dispatches |> List.map (_.Id >> number) |> List.fold max 0) + 1
    $"DSP-%03d{next}"

let private findById what (idOf: 'a -> string) (items: 'a list) id =
    match items |> List.tryFind (fun x -> idOf x = id) with
    | Some x -> Ok x
    | None -> Error(NotFound $"{what} {id} not found")

/// Applies the 5 rules to the requested combination. On success returns the new Dispatch,
/// recorded with its Dispatcher; `addDispatch` puts it on the board.
let dispatch (board: DispatchBoard) (dispatcher: string) (request: DispatchRequest) : Result<Dispatch, DispatchError> =
    let checkRules (order: TransportOrder) (driver: Driver) (tractor: TractorUnit) (trailer: Trailer) =
        let prefixed label reasons =
            reasons |> List.map (fun r -> $"{label}: {r}")

        let reasons =
            prefixed $"Driver {driver.Id} ({driver.Name})" (driverReasons board order driver)
            @ prefixed $"Tractor unit {tractor.Id}" (tractorReasons board order tractor)
            @ prefixed $"Trailer {trailer.Id}" (trailerReasons board order trailer)

        if reasons.IsEmpty then
            Ok
                { Id = nextDispatchId board
                  OrderId = order.Id
                  DriverId = driver.Id
                  TractorId = tractor.Id
                  TrailerId = trailer.Id
                  Dispatcher = dispatcher }
        else
            Error(RulesBroken(order.Id, reasons))

    findById "Order" (fun (o: TransportOrder) -> o.Id) board.Orders request.OrderId
    |> Result.bind (fun order ->
        match board.Dispatches |> List.tryFind (fun d -> d.OrderId = order.Id) with
        | Some existing -> Error(AlreadyDispatched(order.Id, existing.Id))
        | None ->
            match
                findById "Driver" (fun (d: Driver) -> d.Id) board.Drivers request.DriverId,
                findById "Tractor unit" (fun (t: TractorUnit) -> t.Id) board.Tractors request.TractorId,
                findById "Trailer" (fun (t: Trailer) -> t.Id) board.Trailers request.TrailerId
            with
            | Ok driver, Ok tractor, Ok trailer -> checkRules order driver tractor trailer
            | Error e, _, _
            | _, Error e, _
            | _, _, Error e -> Error e)

/// One line about a Dispatch on the board, naming its Dispatcher.
let describeDispatch (board: DispatchBoard) (d: Dispatch) =
    let driver =
        tryFindDriver board d.DriverId
        |> Option.map (fun x -> $"{x.Name} ({x.Id})")
        |> Option.defaultValue d.DriverId

    let route =
        orderOf board d.OrderId
        |> Option.map (fun o -> $", {o.Origin} -> {o.Destination} {describeWindow o.Window}")
        |> Option.defaultValue ""

    $"Dispatched {d.OrderId} as {d.Id}: driver {driver}, tractor unit {d.TractorId}, trailer {d.TrailerId}{route}; dispatched by {d.Dispatcher}."

/// Puts a Dispatch returned by `dispatch` on the board.
let addDispatch (board: DispatchBoard) (dispatch: Dispatch) =
    { board with
        Dispatches = board.Dispatches @ [ dispatch ] }

/// Removes a Dispatch; its order becomes open again.
let cancelDispatch (board: DispatchBoard) (dispatchId: string) : Result<DispatchBoard, string> =
    if board.Dispatches |> List.exists (fun d -> d.Id = dispatchId) then
        Ok
            { board with
                Dispatches = board.Dispatches |> List.filter (fun d -> d.Id <> dispatchId) }
    else
        Error $"Dispatch {dispatchId} not found"
