# Smart Report Assistant — SRA-1 Contract Freeze

Status: frozen for Phase SRA-1. This phase exposes a read-only data snapshot. It does not generate or persist a report and contains no AI integration.

## Public preview API

Direct:

`GET /api/v1/reports/smart-assistant/preview?clubId={clubId}&semesterId={semesterId}`

Gateway:

`GET /gateway/reports/smart-assistant/preview?clubId={clubId}&semesterId={semesterId}`

The existing authenticated `/gateway/reports/{everything}` route forwards the preview endpoint. `clubId` and `semesterId` are required non-empty GUID query values. The actor is always read from the JWT `sub` claim; clients must not send `actorId`.

## Authorization

- Approved `ClubLeader` membership for the exact requested `clubId`: allowed.
- Leader of another club: forbidden.
- Treasurer-only membership: forbidden.
- Student without the approved ClubLeader capability: forbidden.
- `StudentAffairsAdmin`: allowed under the report service's existing read-access policy.

Authorization is evaluated by the canonical `club.access.v1` `SUBMIT_REPORTS` permission before snapshot aggregation.

## Response DTO

`ReportGenerationSnapshot`

| Field | Type | Authoritative source | Rule |
|---|---|---|---|
| `ClubId`, `ClubName` | GUID, string | Club service | Active club matching the exact request |
| `SemesterId`, `SemesterCode` | GUID, string | Report service `Semesters` | Exact active record; missing semester returns not found |
| `TotalMembers` | nullable integer | Club membership | Current active, approved memberships joined by semester end |
| `NewMembers` | nullable integer | Club membership | Above population whose `JoinedAt` is inside semester dates |
| `CompletedEvents` | nullable integer | Club events | Semester-dated events with canonical `Completed` status |
| `CancelledEvents` | nullable integer | Club events | Semester-dated events with canonical `Cancelled` status |
| `ApprovedBudget` | nullable decimal | Finance budget proposals | Sum of authoritative approved amounts for Approved, PartiallyApproved, or Settled proposals dated in semester |
| `ActualExpense` | nullable decimal | Finance settlements | Sum of authoritative actual amounts for Settled proposals dated in semester |
| `RemainingBalance` | nullable decimal | Finance club balance | Current authoritative available balance; not reconstructed per semester |
| `KpiScore`, `KpiRank` | nullable decimal/integer | KPI score ledger/leaderboard | Existing authoritative semester score aggregation and leaderboard ordering; null when the club has no score record |
| `Events` | array | Club events | One normalized item per semester-dated event |
| `FinanceItems` | array | Finance proposals | One normalized item per semester-dated proposal |
| `Sources` | array | Aggregator metadata | Traceable Club, Semester, Event, and Finance identifiers and existing REST routes |
| `Availability` | object | Aggregator | Per-source flags; unavailable values are null and are never replaced with mock/zero fallback |

An empty authoritative collection is represented by an empty list. Nullable scalar metrics remain null when their backing value is absent. In particular, no finance proposal records do not produce a fabricated zero, a missing balance does not produce zero, and a missing KPI score does not produce zero/rank.

## Internal aggregation contract

`smart.reports.v1` is an additive internal gRPC contract:

- `ClubReportSnapshotSource.GetClubReportData`: one batch request returns club identity, membership rows, and semester-dated events.
- `FinanceReportSnapshotSource.GetFinanceReportData`: one batch request returns semester-dated proposals and the current persisted club balance.

The Report service is the only public aggregator. A future AI component must consume this frozen snapshot and must not fan out directly to Club, Finance, KPI, Membership, Event, or Semester services.

No existing Event, Finance REST, KPI, report workflow, or club workflow contract is changed.

## Errors

- Missing/empty `clubId` or `semesterId`: `400 Bad Request`.
- Missing semester: `404 Not Found`.
- Missing club: `404 Not Found`.
- Authenticated actor without exact ClubLeader capability: `403 Forbidden`.
- Missing/invalid identity token: `401 Unauthorized`.
- Internal Club or Finance source unavailable: service-unavailable contract; no mock fallback.

## Known data limits

- Membership storage has current status and `JoinedAt`, but no complete historical status interval. `TotalMembers` is therefore a current-approved population bounded by join date, not a historical end-of-semester reconstruction.
- Finance balance is a current persisted balance and is not semester-scoped by the existing domain.
- Event inclusion uses `ExpectedDate` because the current Event contract has no separate completion timestamp.
- KPI score/rank are null when there is no authoritative score-ledger entry for the club and semester.
