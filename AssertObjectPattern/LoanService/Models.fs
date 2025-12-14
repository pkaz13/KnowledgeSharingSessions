module LoanService.Models

open System

module Customers =
    type Status =
        | Student
        | NotStudent

    type Gender =
        | Male
        | Female

    type Person =
        { Name: string
          Surname: string
          DateOfBirth: DateTime option
          Gender: Gender
          NationalIdentificationNumber: string
          Status: Status }

        member public this.getAge() : int option =
            match this.DateOfBirth with
            | Some dob ->
                let today = DateTime.Today
                Some(today.Year - dob.Year)
            | None -> None

        member public this.isStudent() : bool =
            match this.Status with
            | Student -> true
            | NotStudent -> false

    type Customer =
        { Id: Guid
          Person: Person }

        member public this.isStudent() = this.Person.isStudent ()
        
module Loans =
    type LoanType =
        | Student
        | Regular

module Orders =
    type Promotion = {
        Name: string
        Discount: decimal
    }
    
    type LoanOrder = {
        Customer: Customers.Customer
        LoanType: Loans.LoanType
        Amount: decimal
        InterestRate: decimal
        Commission: decimal
        Promotions: Promotion list
        OrderDate: DateTime
    }