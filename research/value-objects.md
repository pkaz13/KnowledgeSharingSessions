# NDC talk "Just start (with Value Objects)" + F#/C# Value Object patterns

Research for issue #9 (map #4, Topic ValueObjects). Checked 2026-10-02.

Labels used below:
- **[talk]** confirmed from the speaker's own material (slides PDF, recording, her blog).
- **[docs]** confirmed from a primary source (Microsoft Learn, Evans' DDD Reference, Vernon's essays).
- **[verified]** confirmed by compiling and running the code locally (.NET SDK 9.0.301, `dotnet fsi` / `dotnet run`).
- **[general]** general knowledge or a secondary source. Not checked against a primary source.

## Answer (short)

- Speaker: **Katharina Damschen**, Principal Software Architect / "coding architect" at factor10 (Sweden). Before software she was a structural engineer. [talk]
- At NDC Oslo 2026 the talk ran on Thu 17 Sep 2026, 10:20-11:20, Room 3. **Slides exist (PDF)**. The NDC recording was not found yet. A **25 min recording of a shortened version** (Foo Café, Jan 2026) is on YouTube. [talk]
- Running example: a **knitting app**. It starts with `calculateTotalYardage(yardagePerSkein: Long, skeinCount: Int)` and is refactored to `KnittingProject` (Entity, Aggregate Root) → `Skein` (VO) → `Yardage` (VO with unit). Code is in Kotlin. [talk]
- Core argument: VOs are the **cheapest entry point into DDD**. You can "start tomorrow" with no DB-schema or API changes. Making a type explicit makes you ask domain experts **small, answerable questions** (e.g. "what is the max yardage?"). Those questions uncover **implicit concepts**: the expert said "50,000 *meters*", which revealed that "yardage" can be yards or meters → `LengthUnit`. [talk]
- Beyond "Getting Started": anemic models and the Transaction Script layering as the starting pain; "No general rules" (VO vs Entity depends on context); "Value Objects everywhere / No simple types in the domain model"; validate **both upper and lower bounds** (security); a static `validate` returning a result type (not a bool); be careful adding validation to **legacy data**: log first, fix the DB, then enforce. [talk]
- F# gives most VO mechanics for free: structural equality, immutability, cheap single-case DUs, `private` union cases + smart constructor returning `Result`, units of measure (compile-time, zero runtime cost). C# `record` gives value equality and `with`, but **`with` bypasses constructor validation**, `default(record struct)` bypasses validation, and collection members compare by reference. All of these were verified locally.

## 1. The talk

### Sources
| What | URL | Status |
|---|---|---|
| NDC Oslo 2026 agenda page | https://ndcoslo.com/agenda/just-start-with-value-objects | Abstract and bio. No media links. |
| Speaker's "Speaking" page | https://katharina.damschen.net/page/speaking/ | Lists all deliveries and slides. |
| **Slides, NDC Oslo 2026** (44 pages) | https://katharina.damschen.net/files/slides_ndcoslo26.pdf | Primary. Used below. |
| Slides, DDD Europe 2026 (40 pages, 10 Jun 2026) | https://katharina.damschen.net/files/slides_dddeurope26.pdf | Same deck, but without the NDC-only slides listed below. |
| Slides, Scan-Agile 2026 (18 Mar 2026) | https://katharina.damschen.net/files/slides_scanagile26.pdf | Not opened. |
| **Recording, Foo Café Malmö** (22 Jan 2026, uploaded 27 Jan 2026, 23:21) | https://www.youtube.com/watch?v=juZk7On1ucc | Speaker calls it "a somewhat shortened version". Auto-captions were read in full. |
| NDC Oslo 2026 recording | Not found (searched 2026-10-02) | NDC usually publishes on YouTube some weeks later. Re-check before the session. |
| DDD Europe 2026 recording | Not yet ("will link to the recording once it is available") | https://katharina.damschen.net/post/2026-06-reflections-from-dddeurope/ |
| Blog: "My Experiences with Domain Primitives" (10 Nov 2025) | https://katharina.damschen.net/post/2025-11-10-domain-primitives/ | Companion post: the real-project story. |
| Blog: "Implementing Value Objects in Kotlin" (6 Jan 2026) | https://katharina.damschen.net/post/2026-01-06-value-objects-in-kotlin/ | Companion post: implementation details. |
| factor10 profile | https://factor10.com/team/katharina-damschen/ | Bio: DDD, TDD, security. Works in C#, Kotlin, TypeScript. |

### Abstract (NDC page, condensed) [talk]
She inherited "DDD" codebases with layered architecture where domain models only carried data between persistence and application services. Replacing primitives with domain-specific types "reshaped my understanding of the problem space" and "unlocked deeper conversations with domain experts". The talk shows how to build a richer model **bottom-up**, introducing DDD "without overwhelming teams" and getting "past analysis paralysis".

### Narrative flow (NDC slides + Foo Café transcript) [talk]
1. **Personal hook.** As a structural engineer writing apps for engineers, she was the domain expert, alone. Next she was a "coding monkey" taking Jira tasks with no context. She was looking for a middle ground.
2. **Unfamiliar domain on purpose: knitting.** Terms: Knitting Project, Yarn, Skein (usually a fixed *weight*, e.g. 50 g, while the length varies), **Yardage** (the length of yarn). The task: "return the total available yardage for a knitting project".
3. **The 'solution'?** (slide 13)
   ```kotlin
   object YardageCalculator {
     fun calculateTotalYardage(yardagePerSkein: Long, skeinCount: Int) = yardagePerSkein * skeinCount
   }
   ```
   She names three problems: **no validation** (a negative value propagates and fails far away), **no room for further business logic** (e.g. "yardage of the yellow skeins only"), and it **doesn't speak its intent** (the task's language is lost).
4. **DDD overload** (slide 15): Event Storming? UL? Domain Model? Context Map? Tactical patterns? → she starts with the tactical building blocks only.
5. **Entities**: "Objects that are defined primarily by their identity (not by their attributes)." `KnittingProject` gets a UUID, and equals/hashCode use only `id`. Two projects with identical yarn are still different.
6. **Anemic Models** (slide 19): "Domain objects are just data containers. No encapsulation or invariant protection. Services contain all business rules. Overreliance on Entities."
7. **NDC-only: "The Domain Layer"** (slide 20). Two layered-architecture stacks. In the "Anemic Domain Models / Transaction Script" stack the Application layer is fat and Domain is thin. In the "Domain Driven" stack Domain is fat and Application is thin.
8. **Value Objects**: "Objects without a conceptual identity that are primarily defined by their attributes." In real life every skein is distinguishable, but in this app only its yardage matters. **Immutable objects**: "objects whose state cannot be changed after their creation". Benefits named: easier reasoning about states, thread safety, no defensive copying.
9. `data class Skein(val yardage: Long) { init { require(yardage >= 0) {...} } }`. Note the awkward name `yardagePerSkein`, which hinted at the hidden concept "Skein".
10. **"No general rules"** (slide 24): whether something is a VO or an Entity depends on context → **Bounded Context**: "Context in which a domain model is valid."
11. **NDC-only: Ubiquitous Language** (slide 26). It shows a real knitting pattern ("CO 36 sts", "p1, pm, k1, m1r…"), i.e. domain jargon that is opaque to outsiders.
12. **NDC-only: Aggregate** (slides 27-28): "Cluster of associated objects, treated as a unit for the purpose of data changes." Diagram: External Objects → reference → `KnittingProject` (Aggregate Root, Entity) → contains → `Skein` (Value Object).
13. **Putting it all together**: `KnittingProject` has a private constructor and an immutable `skeins: List<Skein>`. `addSkeins(skein, amount)` returns a new project. `calculateTotalAvailableYardage() = skeins.sumOf { it.yardage }`.
14. **"Value Objects everywhere! No simple types in the domain model."** Big entities mirroring DB tables hide VOs. `Skein(val yardage: Long)` becomes `Skein(val yardage: Yardage)` and `value class Yardage(val value: Long)`, with the validation moved into `Yardage`.
15. **Validation extracted** into a static `validate(value): ValidationResult` (sealed `Success` / `Failure(error)`). "use a result wrapper to be able to return a specific message, not just true or false". It can be called before construction.
16. **Upper bound** (security: validate both bounds). `MAX_YARDAGE = 50000L`. A developer cannot answer "what's the max?". The question is cheap and specific, so it is a good one for a domain expert, and it avoids the fear of "dumb questions".
17. **Implicit concept discovered**: the expert says "50,000 *meters*". Why "yardage" then? "Yardage is just the term… it can be yards, it can be meters." → `Yardage(200, LengthUnit.Meters)`. The slide shows a Ravelry screenshot: "273 yards (250 meters)".
18. **Test before/after**: `calculateTotalYardage(200, 5) == 1000` versus `KnittingProject().addSkeins(Skein(Yardage(200, Meters)), 5).calculateTotalAvailableYardage() == Yardage(1000, Meters)`. "reads almost like a book". You "could even show this to your domain experts". Swapped arguments become a compile error.
19. **Lessons from real projects** (slides 35-37): `public class Project { public string Name { get; } }` (C#), then the raw values `198910062382` (a Swedish personal identity number), `info@example.com`, `070-1740699`. The narration for these slides is **not** in the shortened Foo Café recording. The matching story is in her Domain Primitives blog post (see below). I *infer* that it is the same story. [talk, partly inferred]
20. **Advantages** (slide): Immutability. Type safety. Encapsulation of validation and business logic. Better understandability and testability. Explicit domain concepts in code. **A possibility to start asking questions.** Simple classes. **Accessible entry point into DDD.** The first group is "for free". The surprise was the collaboration benefit: colleagues became interested, so "we could start our DDD journey together".
21. **Getting Started** (slide), with spoken detail:
    - *Identify*: replace primitives, especially **primitives that travel together**, and look for **awkward naming** (`yardagePerSkein`).
    - *Validate*: be careful with **legacy code**. New validation can crash the app when it loads old DB rows. "Log those validations first, fix it in your database, and then validate them sharp."
    - *Make immutable*, *implement equality*.

### Companion blog posts [talk]
- **Domain Primitives** (term from Johnsson/Deogun/Sawano, *Secure by Design*; she credits a workshop by Dan Bergh Johnsson and Daniel Deogun). Example: `ProjectName` (max 100 chars, only letters/numbers/whitespace). The `Project` constructor shrinks once `ProjectId`, `ProjectName` and `Description` validate themselves. Validation order: **syntactic first, then semantic**. She avoids regular expressions in validation for security reasons. The "hidden advantage": "Once I made the type explicit I started thinking about its semantics a lot more". That led to stakeholder talks that found an undocumented rule: ProjectName uniqueness depends on project state. VOs are "easy to implement even in legacy systems".
- **VOs in Kotlin**: `data class` (auto equals/hashCode; `init` runs even on `copy`) vs `@JvmInline value class` (zero overhead, allows private constructor + factory). She notes that `init` "is always run, even if an object is created via the copy method". She names this as an advantage over C# records for cross-field invariants. (Section 3.2 shows the C# `with` behaviour, verified.) Sealed interfaces for unions. Operator overloading for `Count + Count`. Pitfall: arrays compare by reference.

## 2. Idiomatic F# Value Objects

### 2.1 Single-case DU [docs]
`type SingleCase = Case of string`: "In F# Discriminated Unions are often used in domain-modeling for wrapping a single type." You can unwrap in a parameter: `let f (ShaderProgram id) = ...`. `[<Struct>]` makes it a value type.
Source: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions

### 2.2 Structural equality and immutability for free [docs][verified]
"Like union and structure types, records have structural equality semantics. Classes have reference equality semantics." "Records are immutable by default" and use `{ r with ... }` for copy-and-update. Opt out with `[<ReferenceEquality>]` (this fits Entities). Unions also support `[<NoEquality>]` / `[<NoComparison>]`.
Source: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records
Verified: a record with an **array** field or a **list** field compares structurally (`true`). This differs from C# records.

### 2.3 Private union cases + smart constructor returning `Result` [docs][verified]
```fsharp
[<Measure>] type m

type Yardage = private Yardage of int64<m>
module Yardage =
    let create (v: int64<m>) : Result<Yardage, string> =
        if v < 0L<m> then Error "Yardage must be positive"
        elif v > 50_000L<m> then Error "Yardage must not exceed 50000 m"
        else Ok (Yardage v)
    let value (Yardage v) = v
```
- `private` means "accessible only from the enclosing type or module" (Access Control docs). Records support the same with `type R = private { ... }` (Records docs, "record with a private constructor").
- Verified: outside the module, `Yardage 5L` **and** the pattern `let f (Yardage v) = v` both fail with **FS1093** "The union cases or fields of the type 'Yardage' are not accessible from this code location". So you must expose a `value` function (or an active pattern / member) for reading.
- Verified: `create 200L<m> = create 200L<m>` → `true`. `create -1L<m>` → `Error "Yardage must be positive"`.
- Sources: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control , https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records

### 2.4 Signature files (.fsi) as an alternative [docs]
"Those language elements that are not listed in the signature file are considered private to the implementation file." The rule matters here: "Records and discriminated unions must expose either all or none of their fields and constructors". So `type Yardage` written without cases in the `.fsi` makes it abstract (opaque), and only the `val create` / `val value` you list are public. This is heavier than `private` (an extra file), but it is the "clean public API" option.
Source: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files

### 2.5 Units of measure [docs][verified]
"Units of measure are used for compile-time unit checking but are not persisted in the run-time environment. Therefore, they do not affect performance." They work on float, decimal, and signed/unsigned integers (int64 included). Docs point to **FSharp.UMX** for unit-tagging non-numeric types (e.g. `string<customerId>`). Units can't be read at runtime (no `ToString` of the unit).
Verified: `1L<m> + 1L<yd>` → **FS0001** "The unit of measure 'yd' does not match the unit of measure 'm'".
Talk link: the "50,000 meters?!" discovery maps directly to `int64<m>` vs `int64<yd>` in F#. That is a strong demo moment. For the logistics domain: `kg`, `m`, `m^3`, etc.
Source: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure

### 2.6 Result-based validation / error accumulation [general]
- `create : raw -> Result<VO, Error>`. Compose with `Result.bind` / `result { }` CE (FsToolkit.ErrorHandling). Use `validation { }` to collect all errors, not just the first. [general]
- Scott Wlaschin, "Designing with types: Single case union types" and "Constrained strings" (2013): wrap primitives, validate in a `create` function, "making illegal states unrepresentable", hide the constructor via signature files. https://fsharpforfunandprofit.com/posts/designing-with-types-single-case-dus/ , https://fsharpforfunandprofit.com/posts/designing-with-types-more-semantic-types/ [secondary but authored by the pattern's main F# proponent]
- Wlaschin, *Domain Modeling Made Functional* (Pragmatic, 2018), ch. "Integrity and Consistency": the `type UnitQuantity = private UnitQuantity of int` + `create`/`value` module pattern. [general, book not re-checked]

### 2.7 F# loopholes worth one honest bullet [verified]
- `[<Struct>]` single-case DU: `Unchecked.defaultof<Weight>` and `Array.zeroCreate` give `Weight 0M` and skip validation. The same struct-default problem exists in C#. Prefer reference DUs for VOs unless you measured a perf need.
- Reflection-based serializers can construct private cases. Validate at the boundary (DTO → `create`). [general]

## 3. C# counterpart and pitfalls

### 3.1 What `record` gives [docs]
- `record` / `record class` is a reference type, `record struct` is a value type. "Positional properties are *immutable* in a `record class` and a `readonly record struct`. They're *mutable* in a `record struct`."
- Compiler-synthesized value equality (`Equals`, `GetHashCode`, `==`, `!=`, `IEquatable<T>`), `with` expressions, `ToString`.
Source: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record

### 3.2 Pitfalls (each verified with `dotnet run`, net9.0) [docs][verified]
| Pitfall | Docs say | Local result |
|---|---|---|
| **`with` skips constructor validation** | `with` "calls the clone method and then sets the properties"; the copy constructor is used, not your primary-constructor logic | `record Yardage(long Meters)` validating in the property initializer: `y with { Meters = -5 }` → `Yardage { Meters = -5 }` |
| Fix: validate in the `init` accessor | — | `init => _m = value is >= 0 and <= 50_000 ? value : throw ...` → `with { Meters = -5 }` throws |
| **`default(T)` / arrays bypass validation** for `record struct` | "For record struct types, a parameterless constructor that sets each field to its default value" | `default(Weight)` and `new Weight[1][0]` → `Weight { Grams = 0 }` |
| **Collection members compare by reference** | Docs example: two records sharing one array are equal; mutating it doesn't change that. Shallow immutability. | `record Skeins(List<int>)` with equal contents → `==` is `False` |
| `record struct` positional props are **mutable** | quoted above | — use `readonly record struct` |
| Inheritance + `EqualityContract` | equality needs the same **runtime** type | — seal VO records |
| Computed property cached at init goes stale after `with` | docs `PointInit` example | — compute on access |
| Records not for EF Core entities | "records and record structs aren't appropriate for use as entity types in Entity Framework Core" | — fine for VOs (owned/complex types) |

### 3.3 Comparison slide material (F# free vs C# work)
| Concern | F# | C# |
|---|---|---|
| Declare a wrapper | `type Sku = private Sku of string` (1 line) | `public sealed record Sku { ... }` with a private ctor and a factory (≈8-12 lines) |
| Value equality | structural by default (records, DUs, lists, **arrays**) | `record`: yes, but collection members compare by reference |
| Immutability | default | `record class` / `readonly record struct` only; shallow |
| Block invalid construction | `private` case → FS1093 at compile time | private ctor works, but `with` / `default` / object initializers need extra care |
| Validation result | `Result<'T,'E>` in FSharp.Core | no built-in Result. Use exceptions, `bool TryCreate(out T)`, or a library |
| Units | units of measure, compile time, zero cost | no language feature. Use a separate type per unit |
| Exhaustive cases | DU + match warning | `switch` on closed hierarchies only from C# 15 (`closed` modifier, per docs) |

## 4. Intro definitions (for slides)

### Evans, *DDD Reference* (2015, CC BY 4.0) [docs]
Source: https://www.domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf
- **DDD in 3 points**: "1. Focus on the core domain. 2. Explore models in a creative collaboration of domain practitioners and software practitioners. 3. Speak a ubiquitous language within an explicitly bounded context." and "DDD addresses both tactical and strategic design."
- **Strategic vs tactical**: the booklet's structure. Part II "Building Blocks of a Model-Driven Design" (Layered Architecture, Entities, Value Objects, Domain Events, Services, Modules, Aggregates, Repositories, Factories) = tactical. Parts IV "Context Mapping for Strategic Design" and V "Distillation for Strategic Design" = strategic. (The labels "tactical/strategic" applied to these parts are common usage. Evans titles only the strategic parts explicitly.)
- **Ubiquitous Language**: "A language structured around the domain model and used by all team members within a bounded context to connect all the activities of the team with the software." Also: "Recognize that a change in the language is a change to the model."
- **Bounded Context**: "A description of a boundary (typically a subsystem, or the work of a particular team) within which a particular model is defined and applicable."
- **Entity**: "Many objects represent a thread of continuity and identity, going through a lifecycle, though their attributes may change." … "The model must define what it means to be the same thing."
- **Value Object**: "Some objects describe or compute some characteristic of a thing." … "When you care only about the attributes and logic of an element of the model, classify it as a value object. Make it express the meaning of the attributes it conveys and give it related functionality. Treat the value object as immutable. Make all operations Side-effect-free Functions…"
- **Aggregate**: "Cluster the entities and value objects into aggregates and define boundaries around each. Choose one entity to be the root of each aggregate, and allow external objects to hold references to the root only…" The talk's wording ("Cluster of associated objects, treated as a unit for the purpose of data changes") is Evans' 2003 book definition. [general]

### Vernon
- *Effective Aggregate Design* (2011, dddcommunity.org; primary) [docs]. Rules: "Model True Invariants In Consistency Boundaries", "Design Small Aggregates", "Reference Other Aggregates By Identity", "Use Eventual Consistency Outside the Boundary". On VOs: "If instances can be completely replaced, it points to the use of a value object rather than an entity… many concepts modeled as entities can be refactored to value objects." "Value objects are smaller and safer to use (fewer bugs). Due to immutability it is easier for unit tests to prove their correctness."
  https://www.dddcommunity.org/library/vernon_2011/ (Part I PDF: https://www.dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_1.pdf, Part II: …/Vernon_2011_2.pdf)
- *Implementing DDD* (2013), ch. 6. VO characteristics: **Measures, Quantifies, or Describes**; **Immutable**; **Conceptual Whole**; **Replaceability**; **Value Equality**; **Side-Effect-Free Behavior**. [general: the O'Reilly pages are paywalled (403); the first two headings and their wording were confirmed via search snippets of the O'Reilly TOC]

## 5. Implications for the ValueObjects Topic (suggestions, not decisions)
- Reuse the talk's arc with logistics instead of knitting. For example `calculateTotalWeight(weightPerPallet: decimal, palletCount: int)` → `Shipment` (Entity/Aggregate Root) → `Pallet` (VO) → `Weight` (VO, `decimal<kg>`). Then the "max weight? … in kg or lb?" question reproduces the implicit-concept discovery.
- Keep her two strongest non-obvious points: (1) VOs make you ask **small, answerable questions** of domain experts. (2) **Legacy data**: log validation failures before enforcing.
- Credit: cite Katharina Damschen / factor10 and link the NDC slides PDF and the Foo Café video.
- Open: the NDC Oslo 2026 recording URL (re-check NDC's YouTube channel before the session).
