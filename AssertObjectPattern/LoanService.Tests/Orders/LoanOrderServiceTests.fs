module LoanService.Tests.Orders.LoanOrderServiceTests

open System
open LoanService
open LoanService.Models.Customers
open LoanService.Models.Orders
open Shouldly
open Xunit

let aStudent () =
    let person: Person = {
        Name = "Piotr"
        Surname = "Kazmierczak"
        DateOfBirth = DateTime(1995, 1, 1) |> Some
        Gender = Male
        NationalIdentificationNumber = "1234567890"
        Status = Student
    }
    
    let customer: Customer = {
        Id = Guid.NewGuid()
        Person = person
    }
    
    customer
    
let ok (result: Result<LoanOrder, 'a>) =
    match result with
    | Ok value -> value
    | Error _ -> failwith "Expected Ok result"
    
    
[<Fact>]
let ``Should create student loan order`` () =
    let student = aStudent()
    
    let loanOrder = LoanOrderService.studentLoadOrder student |> ok
    
    loanOrder.OrderDate.ShouldBe(DateTime.Today)
    loanOrder.Promotions.ShouldContain(fun p -> p.Name = "Student Loan Promotion")
    loanOrder.Promotions[0].Discount.ShouldBe 10.00M
        
[<Fact>]
let ``Should create student loan order using assert object`` () =
    let student = aStudent()
    
    let loanOrder = LoanOrderService.studentLoadOrder student |> ok
    
    let orderShould = LoanOrderAssert(loanOrder)
    orderShould.BeRegisteredToday() |> ignore
    orderShould.HavePromotion("Student Loan Promotion") |> ignore
    orderShould.HaveOnFirstPromotionDiscountValueOf(10.00M) |> ignore
    orderShould.HaveOnlyOnePromotion() |> ignore

[<Fact>]
let ``Should create student loan order using chained assert object`` () =
    let student = aStudent()
    
    let loanOrder = LoanOrderService.studentLoadOrder student |> ok
    
    loanOrder
        .Should()
        .BeRegisteredToday()
        .HavePromotion("Student Loan Promotion")
        .HaveOnFirstPromotionDiscountValueOf(10.00M)
        .HaveOnlyOnePromotion() |> ignore
        
[<Fact>]
let ``Should create correct student loan order using simple assertion`` () =
    let student = aStudent()
    
    let loanOrder = LoanOrderService.studentLoadOrder student |> ok
    
    loanOrder.Should().BeCorrect() |> ignore
