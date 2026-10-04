/// Who is dispatching, taken from the authenticated user of the request.
module Logistics.Api.Dispatcher

open System.Security.Claims

/// Role that may dispatch (`dispatch_order`). Keycloak gives it to alice, not bob;
/// the shared API key grants it: one key, full access, no per-user identity.
[<Literal>]
let Role = "dispatcher"

/// `preferred_username` (OAuth), else the identity name (e.g. `api-key`), else `anonymous`.
let nameOf (user: ClaimsPrincipal) =
    match user with
    | null -> "anonymous"
    | u when isNull u.Identity || not u.Identity.IsAuthenticated -> "anonymous"
    | u ->
        match u.FindFirst "preferred_username" with
        | null when not (System.String.IsNullOrEmpty u.Identity.Name) -> u.Identity.Name
        | null -> "anonymous"
        | claim -> claim.Value
