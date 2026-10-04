module Logistics.Api.Tests.Adapters.HealthTests

open System.Net
open Microsoft.AspNetCore.Mvc.Testing
open Swensen.Unquote
open Xunit

[<Fact>]
let ``GET /health returns 200`` () =
    task {
        use factory = new WebApplicationFactory<Logistics.Api.Program.Marker>()
        use client = factory.CreateClient()

        let! response = client.GetAsync("/health", TestContext.Current.CancellationToken)

        test <@ response.StatusCode = HttpStatusCode.OK @>
    }
