module LoanService.LoanOrderService

open System
open LoanService.Models
open LoanService.Models.Loans
open LoanService.Models.Orders

let studentLoadOrder (customer: Customers.Customer) =
    match customer.isStudent () with
    | false -> Error "Cannot order a student loan if customer is not a student"
    | true ->
        let now = DateTime.Today

        let promotion: Promotion =
            { Name = "Student Loan Promotion"
              Discount = 10.00M }

        let loanOrder: LoanOrder =
            { Customer = customer
              LoanType = LoanType.Student
              OrderDate = now
              InterestRate = 0.05M
              Amount = 1000.00M
              Commission = 200.00M
              Promotions = [ promotion ] }

        Ok loanOrder
