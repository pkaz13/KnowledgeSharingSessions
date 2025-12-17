module LoanService.Tests.Orders.LoanOrderServiceTests

open System
open LoanService
open LoanService.Models.Customers
open LoanService.Models.Orders
open FsUnit.Xunit
open FsUnit.CustomMatchers
open Xunit


type ``Creation of student loan order`` () = 

    let aStudent () =
        let person: Person =
            { Name = "Piotr"
              Surname = "Kazmierczak"
              DateOfBirth = DateTime(1995, 1, 1) |> Some
              Gender = Male
              NationalIdentificationNumber = "1234567890"
              Status = Student }

        let customer: Customer = { Id = Guid.NewGuid(); Person = person }

        customer
        
    [<Fact>]
    let ``succeeds if registered today`` () =
        
        aStudent ()
        |> LoanOrderService.studentLoadOrder
        |> Result.map (fun lo -> lo.OrderDate |> should equal DateTime.Today)
        |> Result.mapError (fun errMsg ->
            Assert.Fail($"Expected successful loan order creation but got error: {errMsg}")
        )

    [<Fact>]
    let ``succeeds if has a promotion`` () =
        
        aStudent ()
        |> LoanOrderService.studentLoadOrder
        |> Result.map (fun lo ->
            lo.Promotions
            |> List.map _.Name            
            |> should contain "Student Loan Promotion")
        |> Result.mapError (fun errMsg ->
            // is not expected to happen
            Assert.Fail($"Expected successful loan order creation but got error: {errMsg}")
        )
            
    [<Fact>]
    let ``succeeds if has promotion is 10.0M`` () =
        let discount = 10.0M
        aStudent ()
        |> LoanOrderService.studentLoadOrder
        |> Result.map (fun lo ->
            lo.Promotions
            |> List.head
            |> (fun d -> d.Discount |> should equal discount))
        |> Result.mapError (fun errMsg ->
            // is not expected to happen
            Assert.Fail($"Expected successful loan order creation but got error: {errMsg}")
        )

    [<Theory>]
    [<InlineData(9.0)>]
    [<InlineData(11.0)>]
    let ``fails if promotion is below or above 10.0M`` (discount) =
        
        aStudent ()
        |> LoanOrderService.studentLoadOrder
        |> Result.map (fun lo ->
            lo.Promotions
            |> List.head
            |> (fun d -> d.Discount = discount |> should be False))
        |> Result.mapError (fun errMsg ->
            // is not expected to happen
            Assert.Fail($"Expected successful loan order creation but got error: {errMsg}")
        )


