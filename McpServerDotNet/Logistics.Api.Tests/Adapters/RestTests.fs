module Logistics.Api.Tests.Adapters.RestTests

open System.Net
open System.Text.Json
open Microsoft.AspNetCore.Mvc.Testing
open Swensen.Unquote
open Xunit

[<Fact>]
let ``GET /orders/{id}/dispatch-options returns 200 and JSON`` () =
    task {
        use factory = new WebApplicationFactory<Logistics.Api.Program.Marker>()
        use client = factory.CreateClient()

        let! response = client.GetAsync("/orders/ORD-103/dispatch-options", TestContext.Current.CancellationToken)
        let! body = response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        use json = JsonDocument.Parse body

        test <@ response.StatusCode = HttpStatusCode.OK @>
        test <@ response.Content.Headers.ContentType.MediaType = "application/json" @>
        test <@ json.RootElement.GetProperty("orderId").GetString() = "ORD-103" @>
    }
