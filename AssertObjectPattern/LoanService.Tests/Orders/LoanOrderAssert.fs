namespace LoanService.Tests.Orders

open System
open LoanService.Models.Orders
open Shouldly

type LoanOrderAssert(loanOrder: LoanOrder) =

    let havePromotionCount count =
        loanOrder.Promotions.Length.ShouldBe count

    member this.BeRegisteredToday() =
        loanOrder.OrderDate.ShouldBe(DateTime.Today)

        this

    member this.HavePromotion(promotionName: string) =
        loanOrder.Promotions.ShouldContain(fun p -> p.Name = promotionName)

        this

    member this.HaveOnlyOnePromotion() =
        havePromotionCount 1

        this

    member this.HaveOnFirstPromotionDiscountValueOf(discountValue: decimal) =
        loanOrder.Promotions[0].Discount.ShouldBe discountValue

        this

    member this.BeCorrect() =
        this.BeRegisteredToday().HaveOnlyOnePromotion().HaveOnFirstPromotionDiscountValueOf 10.00M

[<AutoOpen>]
module LoanAssertExtensions =
    type LoanOrder with
        member this.Should() =
            LoanOrderAssert(this)

