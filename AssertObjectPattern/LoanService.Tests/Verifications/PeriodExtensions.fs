namespace LoanService.Tests.Verifications

open NodaTime

[<AutoOpen>]
module PeriodExtensions =
    type System.Int32 with
        member this.Years() =
            Period.FromYears(this)
