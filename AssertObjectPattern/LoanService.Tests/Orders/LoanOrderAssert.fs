namespace LoanService.Tests.Orders

open System
open LoanService.Models.Orders
open Shouldly
open Xunit

type LoanOrderAssert(loanOrder: Result<LoanOrder, string>) =

    let havePromotionCount count =
        match loanOrder with
        | Error _ -> Assert.Fail("Loan order was expected to be successful")
        | Ok loanOrder -> loanOrder.Promotions.Length.ShouldBe count

    member this.BeRegisteredToday() =
        match loanOrder with
        | Error _ ->
            Assert.Fail("Loan order was expected to be successful")

            this
        | Ok loanOrder ->
            loanOrder.OrderDate.ShouldBe(DateTime.Today)

            this

    member this.HavePromotion(promotionName: string) =
        match loanOrder with
        | Error _ ->
            Assert.Fail("Loan order was expected to be successful")

            this
        | Ok loanOrder ->
            loanOrder.Promotions.ShouldContain(fun p -> p.Name = promotionName)

            this

    member this.HaveOnlyOnePromotion() =
        havePromotionCount 1

        this

    member this.HaveOnFirstPromotionDiscountValueOf(discountValue: decimal) =
        match loanOrder with
        | Error _ ->
            Assert.Fail("Loan order was expected to be successful")
            this
        | Ok loanOrder ->
            loanOrder.Promotions[0].Discount.ShouldBe discountValue

            this

    member this.BeCorrect() =
        this.BeRegisteredToday().HaveOnlyOnePromotion().HaveOnFirstPromotionDiscountValueOf 10.00M
