namespace LoanService.Tests.Verifications

open Shouldly
open Xunit

type VerificationAssert(verificationResult: Result<bool, string>) =

    member this.ShouldBeSuccessful() =
        match verificationResult with
        | Ok result -> result.ShouldBe true
        | Error _ -> Assert.Fail("Age verification was expected to be successful")

        this

    member this.ShouldFail() =
        match verificationResult with
        | Ok result -> result.ShouldBe false
        | Error msg -> msg.ShouldBe("Age cannot be negative", null)

        this