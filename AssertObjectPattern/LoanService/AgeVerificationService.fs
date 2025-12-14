module LoanService.AgeVerificationService

open LoanService.Models.Customers

let passesFor (person: Person) =
    let age = person.getAge() |> Option.defaultValue -1
    
    if age <= 0 then
        Error "Age cannot be negative"
    else
        Ok (age >= 18 && age <= 99)
         