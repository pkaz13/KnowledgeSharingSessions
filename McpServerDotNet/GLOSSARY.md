# McpServerDotNet demo domain

The simplified road-haulage dispatch domain behind the demo API: which Driver, Tractor unit and Trailer take which Transport Order. Terms for the repo itself (Topic, Session, Script) are in the [root GLOSSARY](../GLOSSARY.md).

## Language

### Resources

**Driver**:
A person who drives a Tractor unit, holding a C+E licence, a Driver CPC and optionally an ADR certificate, each with an expiry date.
_Avoid_: Crew (in trucking it means team drivers), employee

**Tractor unit**:
The truck that pulls a Trailer; either Standard or Low deck. A Mega trailer needs a Low deck tractor unit.
_Avoid_: Truck, vehicle, tractor (outside code IDs like `TRK-01`)

**Trailer**:
The cargo unit pulled by a Tractor unit; one of Curtainsider, Mega or Reefer.
_Avoid_: Semi, container

**Absence**:
A period when a Driver is unavailable (sick leave or holiday); counts as busy for every rule.
_Avoid_: Leave, time off

### Work

**Transport Order**:
A load to move from origin to destination within a time window, with a cargo type (General, Foodstuffs, Dangerous goods) and a required Trailer type.
_Avoid_: Job, shipment, order (alone, when ambiguous)

**Dispatch**:
The assignment of one Driver, one Tractor unit and one Trailer to a Transport Order for its window; a Transport Order has at most one.
_Avoid_: Booking, assignment, allocation

**Dispatch option**:
One combination of Driver, Tractor unit and Trailer that would satisfy every rule for a given Transport Order.
_Avoid_: Candidate, suggestion

**Dispatch board**:
The whole state of the demo: all Drivers, Tractor units, Trailers, Transport Orders and Dispatches, as one immutable value.
_Avoid_: Database, planning board

**Dispatcher**:
Who created a Dispatch: the signed-in user's name in `OAuth` mode, `api-key` in `ApiKey` mode, `anonymous` in `None` mode. Also the Keycloak role required to call `dispatch_order`.
_Avoid_: Planner, user
