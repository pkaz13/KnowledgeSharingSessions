/// Tiny hand-built Dispatch boards for rule tests. Everything happens on 2026-10-04.
module Logistics.Api.Tests.Domain.Boards

open System
open Logistics.Api.Domain

let at (hour: int) =
    DateTimeOffset(2026, 10, 4, hour, 0, 0, TimeSpan.Zero)

let farFuture = DateOnly(2030, 1, 1)

let driver id =
    { Id = id
      Name = id
      CeLicenceExpiry = farFuture
      DriverCpcExpiry = farFuture
      AdrCertificateExpiry = None
      Absences = [] }

let tractor id kind =
    { Id = id
      Registration = id
      Kind = kind }

let trailer id trailerType =
    { Id = id
      Registration = id
      Type = trailerType }

/// Window 08:00-16:00.
let order id cargo trailerType =
    { Id = id
      Origin = "Poznan"
      Destination = "Berlin"
      Cargo = cargo
      RequiredTrailer = trailerType
      Window = { From = at 8; To = at 16 } }

let dispatch id (o: TransportOrder) driverId tractorId trailerId =
    { Id = id
      OrderId = o.Id
      DriverId = driverId
      TractorId = tractorId
      TrailerId = trailerId
      Dispatcher = "anonymous" }

let board drivers tractors trailers orders dispatches =
    { Drivers = drivers
      Tractors = tractors
      Trailers = trailers
      Orders = orders
      Dispatches = dispatches }

let optionsFor board orderId =
    match Logistics.Api.Domain.findDispatchOptions board orderId with
    | Ok options -> options
    | Error e -> failwith (DispatchError.describe e)

let option driverId tractorId trailerId =
    { DriverId = driverId
      DriverName = driverId
      TractorId = tractorId
      TrailerId = trailerId }
