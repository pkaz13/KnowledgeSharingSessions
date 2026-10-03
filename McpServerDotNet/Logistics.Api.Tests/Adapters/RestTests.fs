module Logistics.Api.Tests.Adapters.RestTests

open System
open System.Net
open System.Net.Http.Json
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

[<Fact>]
let ``POST /dispatches dispatches, refuses a broken rule with text, and DELETE /dispatches/{id} cancels`` () =
    task {
        use factory = new WebApplicationFactory<Logistics.Api.Program.Marker>()
        use client = factory.CreateClient()
        let ct = TestContext.Current.CancellationToken

        let post orderId driverId =
            client.PostAsJsonAsync(
                "/dispatches",
                {| orderId = orderId
                   driverId = driverId
                   tractorId = "TRK-02"
                   trailerId = "TRL-02" |},
                ct
            )

        let! created = post "ORD-103" "D-01"
        let! createdBody = created.Content.ReadAsStringAsync(ct)
        use json = JsonDocument.Parse createdBody

        let! refused = post "ORD-103" "D-01"
        let! refusedBody = refused.Content.ReadAsStringAsync(ct)

        let! deleted = client.DeleteAsync("/dispatches/DSP-005", ct)
        let! deletedAgain = client.DeleteAsync("/dispatches/DSP-005", ct)

        test <@ created.StatusCode = HttpStatusCode.Created @>
        test <@ created.Headers.Location = Uri("/dispatches/DSP-005", UriKind.Relative) @>
        test <@ json.RootElement.GetProperty("dispatcher").GetString() = "anonymous" @>
        test <@ refused.StatusCode = HttpStatusCode.Conflict @>
        test <@ refusedBody = "Order ORD-103 is already dispatched (DSP-005)" @>
        test <@ deleted.StatusCode = HttpStatusCode.NoContent @>
        test <@ deletedAgain.StatusCode = HttpStatusCode.NotFound @>
    }
