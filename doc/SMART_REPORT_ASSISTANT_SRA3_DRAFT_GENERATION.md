# Smart Report Assistant — SRA-3 Rule-Based Draft Generation

Status: ready for deterministic, in-memory draft generation. SRA-3 contains no AI integration and does not create, save, update, or submit a Report entity.

## API contract

Direct:

`POST /api/v1/reports/smart-assistant/generate`

Gateway:

`POST /gateway/reports/smart-assistant/generate`

The existing authenticated `/gateway/reports/{everything}` route forwards the endpoint.

Request:

```json
{
  "clubId": "00000000-0000-0000-0000-000000000000",
  "semesterId": "00000000-0000-0000-0000-000000000000",
  "reportType": 3
}
```

Only the three current Report types are accepted:

- `1`: Financial
- `2`: Activity
- `3`: General

Actor identity comes from JWT `sub`. The API does not accept actor role, KPI, Finance totals, event counts, membership counts, or other source metrics from the client.

## Draft contract

`GeneratedReportDraft`:

- `clubId`
- `semesterId`
- `reportType`
- `generatedTitle`
- `generatedContent`
- `sources`
- `validation`
- `generatorType`: `RuleBased`
- `snapshotVersion`: `sra-1.v1`
- `generatedAt`

There is no generated Summary because the current Report entity and create/update contracts have no Summary field.

## Generation rules

### Title and introduction

Titles use the current ReportType plus authoritative SemesterCode and ClubName:

- `Báo cáo tài chính học kỳ {SemesterCode} - {ClubName}`
- `Báo cáo hoạt động học kỳ {SemesterCode} - {ClubName}`
- `Báo cáo tổng hợp học kỳ {SemesterCode} - {ClubName}`

The introduction states that the content is a system-data summary. It does not claim exceptional success, external recognition, or qualitative outcomes.

### Membership

The section uses only `TotalMembers` and `NewMembers` from the snapshot. It states that totals are based on current approved/active membership and JoinedAt. Missing metrics are described as unavailable and are never replaced with zero.

### Events

The section uses authoritative CompletedEvents, CancelledEvents, and Event records:

- Cancelled events are not described as completed achievements.
- Only Completed events are listed as completed activity.
- At most ten events are listed.
- Ordering is ExpectedDate ascending, then Id.
- Displayed date is explicitly the expected date.
- No participant count, evidence, outcome, or completion date is invented.

When there is no Completed event, the draft says that no completed activity has been recorded for the semester.

### Finance

The section displays snapshot-level ApprovedBudget, ActualExpense, and RemainingBalance directly. It does not recompute aggregate metrics from FinanceItems.

FinanceItems are shown only as source details, ordered by ProposedDate then Id, with a maximum of ten entries. The generator does not infer settlement, create transactions, or replace null with zero.

RemainingBalance is always described as the current persisted club balance and never as a semester-specific balance.

Money uses an explicit Vietnamese format and the `VNĐ` suffix, independent of the machine's current locale.

### KPI

KpiScore and KpiRank are shown only when both authoritative values exist. If either is unavailable, the draft states that KPI has not been recorded. The generator never calculates KPI, replaces null with zero, or infers rank.

### Notes

The generated notes remain short and disclose:

- Membership history limitation.
- ExpectedDate/completion timestamp limitation.
- Current-balance limitation when balance is present.
- KPI null behavior when KPI is unavailable.

## Provenance

The response reuses the exact `Sources` collection from the SRA-1 snapshot. The generator does not create, rewrite, sort, or fabricate source URLs.

Current SRA-1 source references may include Club, Semester, Event, and Finance records. KPI and Membership references are not invented when the snapshot has no corresponding SourceReference.

## Validation integration

The generation orchestrator:

1. Fetches the SRA-1 snapshot exactly once.
2. Generates title/content in memory.
3. Constructs the real SRA-2 validation payload from generated title/content plus requested ClubId, SemesterId, and ReportType.
4. Runs the existing `IReportValidationEngine` directly against the same snapshot.
5. Returns draft, sources, and validation together.

It does not call `IReportValidationService`, because that service would fetch the snapshot a second time. SRA-2 severity and `IsReadyToSubmit` semantics remain unchanged.

## Determinism

For the same snapshot and ReportType:

- Title and content are identical.
- Event order is ExpectedDate then Id.
- Finance order is ProposedDate then Id.
- Section order is fixed.
- Money and date formatting use explicit cultures/formats.
- No random identifier, dictionary iteration, or machine-local culture affects content.
- Only `GeneratedAt` may vary.

## Authorization and errors

- Approved ClubLeader with canonical `SUBMIT_REPORTS` permission for the exact ClubId: allowed.
- Cross-club Leader: `403`.
- Treasurer-only: `403`.
- Student without Leader capability: `403`.
- StudentAffairsAdmin: allowed under the existing Report read/generate policy.
- Anonymous: `401`.
- Empty/invalid ClubId or SemesterId: `400`.
- Undefined ReportType: `400`.
- Missing semester: `404`.
- Required snapshot dependency unavailable: `503`; no empty/fake draft is returned.

## Data limitations

- Membership has no historical status intervals.
- RemainingBalance is current and not semester-scoped.
- Event has ExpectedDate but no completion timestamp or evidence metadata.
- Report has no structured event/finance references and no Summary.
- KPI may be absent from the backend ledger/leaderboard.

## Tests

SRA-3 tests cover all Report types, title/section generation, deterministic output and ordering, no-completed-event behavior, KPI/Finance/member limitations, current-balance wording, no invented participants/evidence/completion date, null handling, stable Vietnamese money format, Unicode, exact source preservation, validation integration, single snapshot call, error propagation, ReportType validation, and exact-club authorization.

## Remaining gaps

- No frontend integration.
- No AI provider or AI abstraction.
- No report persistence or auto-save.
- No auto-submit.
- Validation is not enforced in the submit workflow.
- No event evidence/source-link model.
