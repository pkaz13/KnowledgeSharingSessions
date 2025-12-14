module LoanService.Tests.Verifications.AgeVerificationServiceTests

open LoanService
open LoanService.Models.Customers
open NodaTime
open NodaTime.Extensions
open Xunit

let todayUtc : LocalDate =
    SystemClock.Instance
        .InUtc()
        .GetCurrentDate()

let personWhoHasLivedFor (age: Period) =
    let birthDate = (todayUtc - age).ToDateTimeUnspecified()
    
    {
        Name = "Piotr"
        Surname = "Kazmierczak"
        DateOfBirth = birthDate |> Some
        Gender = Male
        NationalIdentificationNumber = "1234567890"
        Status = NotStudent
    }
    
[<Fact>]
let ``Verification should pass for age between 18 and 99`` () =
    let person = personWhoHasLivedFor ((30).Years())
    
    let verificationResult = AgeVerificationService.passesFor person
    
    let verification = VerificationAssert(verificationResult)
    verification.ShouldBeSuccessful() |> ignore
    
[<Fact>]
let ``Verification should fail when person is older than 99`` () =
    let person = personWhoHasLivedFor ((100).Years())
    
    let verificationResult = AgeVerificationService.passesFor person
    
    let verification = VerificationAssert(verificationResult)
    verification.ShouldFail() |> ignore

[<Fact>]
let ``Verification should fail when person is younger than 18`` () =
    let person = personWhoHasLivedFor ((17).Years())
    
    let verificationResult = AgeVerificationService.passesFor person
    
    let verification = VerificationAssert(verificationResult)
    verification.ShouldFail() |> ignore
    
[<Fact>]
let ``Verification should fail when person's age is negative`` () =
    let person = personWhoHasLivedFor ((-1).Years())
    
    let verificationResult = AgeVerificationService.passesFor person
    
    let verification = VerificationAssert(verificationResult)
    verification.ShouldFail() |> ignore