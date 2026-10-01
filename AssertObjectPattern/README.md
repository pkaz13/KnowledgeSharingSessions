# Assert Object Pattern

Wrap low-level, technical assertions in an **assert object** whose methods speak the language of the business, so a test reads like a specification: `orderShould.BeRegisteredToday().HavePromotion("Student Loan Promotion")` instead of a list of field comparisons.

**Stack:** F#, .NET 8, xUnit, Shouldly, NodaTime

## What's inside

| Path | What it shows |
|------|---------------|
| `LoanService/` | A tiny loan-ordering and age-verification domain under test. |
| `LoanService.Tests/Orders/LoanOrderServiceTests.fs` | The same test written three ways: raw assertions, an assert object, and a chained assert object. Plus a single high-level `BeCorrect()` assertion that bundles the others. |
| `LoanService.Tests/Orders/LoanOrderAssert.fs` | The assert object for loan orders. Each method returns `this`, so calls can be chained. |
| `LoanService.Tests/Verifications/` | A second, smaller example of the pattern for age verification. |
| `materials/` | Slides (`.pptx` and `.pdf`). |

## Run it

Requires the .NET 8 SDK or newer.

```sh
cd AssertObjectPattern
dotnet test
```

## Sessions

| Date | Audience | Notes |
|------|----------|-------|
| 2025-12 | Internal team | First delivery. |
