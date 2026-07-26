# Smart Report Assistant — SRA-2 Rule-Based Validation

Status: ready for deterministic pre-submit validation. SRA-2 does not generate, persist, update, or submit a report and contains no AI integration.

## API

Direct:

`POST /api/v1/reports/smart-assistant/validate`

Gateway:

`POST /gateway/reports/smart-assistant/validate`

The existing authenticated `/gateway/reports/{everything}` route forwards this endpoint.

Request:

```json
{
  "clubId": "guid",
  "semesterId": "guid",
  "reportType": 3,
  "title": "Báo cáo học kỳ",
  "content": "Nội dung báo cáo",
  "attachments": [
    {
      "url": "https://example.invalid/evidence",
      "fileName": "evidence.pdf"
    }
  ]
}
```

The payload mirrors the existing create-report fields. There is no `summary` field in the current Report entity/DTO. Attachments are report-level and are not treated as event evidence because no source relationship exists.

The server reads the actor from JWT `sub`, authorizes the exact `clubId`, fetches the SRA-1 snapshot once, and evaluates all rules against that immutable context. Client-provided KPI, Finance totals, roles, actor IDs, or reviewer IDs are not accepted.

## Validation result

`ReportValidationIssue`:

- `code`: stable machine-readable rule code.
- `severity`: `Error`, `Warning`, or `Suggestion`.
- `message`: Vietnamese presentation message.
- `field`: optional request field.
- `sourceType`, `sourceId`, `sourceTitle`: optional authoritative source identity.
- `suggestedAction`: optional remediation guidance.

`ReportValidationResult`:

- `isReadyToSubmit`
- `errors`
- `warnings`
- `suggestions`
- `evaluatedAt`
- `clubId`
- `semesterId`
- `snapshotVersion`: `sra-1.v1`
- `availability`: unchanged SRA-1 availability flags

Issues retain registration order by rule group. Source-backed event and finance issues are ordered by source date then ID. Duplicate `(code, field, sourceType, sourceId)` identities are removed.

## Severity and blocking

- `Error`: `isReadyToSubmit = false`.
- `Warning`: does not block by default.
- `Suggestion`: informational guidance only.
- No errors: `isReadyToSubmit = true`.

This result is advisory in SRA-2. The existing submit endpoint is unchanged and does not enforce the engine.

## Implemented rules

| Code | Severity | Behavior |
|---|---|---|
| `REPORT_CLUB_REQUIRED` | Error | Missing ClubId in engine input |
| `REPORT_SEMESTER_REQUIRED` | Error | Missing SemesterId in engine input |
| `REPORT_TYPE_REQUIRED` | Error | Missing/undefined ReportType |
| `REPORT_TITLE_REQUIRED` | Error | Blank title |
| `REPORT_CONTENT_REQUIRED` | Error | Blank content |
| `REPORT_CONTENT_TOO_SHORT` | Warning | Content shorter than centralized 100-character recommendation |
| `EVENT_CANCELLED_EXCLUDED` | Suggestion | Confirms a cancelled source event is excluded from completed activity |
| `EVENT_PAST_DUE_NOT_COMPLETED` | Warning | ExpectedDate is past and status is neither Completed nor Cancelled |
| `FINANCE_ACTUAL_EXCEEDS_APPROVED` | Error | Actual amount exceeds approved amount on the same proposal |
| `FINANCE_UNSETTLED_APPROVED_PROPOSAL` | Warning | Approved/PartiallyApproved proposal is not Settled |
| `FINANCE_UNAPPROVED_EXCLUDED` | Suggestion | Draft/Pending/Rejected proposal is excluded from approved budget |
| `FINANCE_DATA_UNAVAILABLE` | Warning | Optional Finance snapshot is marked unavailable |
| `MEMBERSHIP_DATA_LIMITED` | Suggestion | Explains the lack of historical membership intervals |
| `KPI_UNAVAILABLE` | Suggestion | Authoritative KPI score or rank is null/unavailable |

The 100-character content threshold is a recommendation only because the current Report persistence contract requires non-empty content but defines no minimum length.

## Deferred rules: insufficient current contract

The following codes are intentionally not implemented:

- `REPORT_SUMMARY_REQUIRED`: Report has no Summary field.
- `EVENT_NOT_COMPLETED`: report content has no structured event references or claimed-completed flag; text parsing would be unreliable.
- `EVENT_OUTSIDE_SEMESTER`: SRA-1 only exposes semester-filtered events and request contains no event IDs.
- `EVENT_MISSING_EVIDENCE`: Event and snapshot contracts contain no evidence URL/metadata; report attachments are not event-linked.
- `FINANCE_RECEIPT_MISSING`: Finance snapshot does not expose ReceiptUrl. SRA-2 does not change the Finance contract.
- Per-member `MEMBERSHIP_PENDING_EXCLUDED` and `MEMBERSHIP_REJECTED_EXCLUDED`: the public SRA-1 snapshot exposes authoritative approved totals, not raw membership rows.

These rules must not be approximated by parsing prose, matching titles, inventing participant counts, or treating generic attachments as event evidence.

## Authorization and errors

- Approved ClubLeader with canonical `SUBMIT_REPORTS` permission for the exact club: allowed.
- Leader of another club: `403`.
- Treasurer-only: `403`.
- Student without Leader capability: `403`.
- StudentAffairsAdmin: allowed under current Report read/validation policy.
- Anonymous: `401` from `[Authorize]`.
- Empty ClubId/SemesterId at API boundary: `400`.
- Missing semester: `404`, propagated from SRA-1.
- Required snapshot dependency unavailable: `503`, not converted into fake validation issues.
- Invalid JSON/GUID/enum shape: standard API validation `400`.

## Source limitations

- Membership totals use current approved/active status plus JoinedAt; historical membership intervals do not exist.
- RemainingBalance is persisted current balance, not semester balance, and no validation rule treats it as semester balance.
- ExpectedDate is the only event date available; it is not a completion timestamp.
- KPI values come only from the backend ledger/leaderboard. Null remains null; rank is never inferred from a client value.
- Rules never create transactions, balances, settlements, event completion timestamps, KPI values, or mock fallback.

## Test coverage

SRA-2 tests cover completeness, short content, cancelled and past-due event behavior, finance overrun/unsettled/unapproved behavior, optional availability, membership/KPI limitations, blocking semantics, deterministic ordering, duplicate suppression, single snapshot call, dependency/semester error propagation, exact-club authorization, Treasurer/Student denial, Admin policy, and invalid identifiers.

## Remaining gaps

- No structured report-to-event or report-to-finance source references.
- No event evidence metadata.
- No membership status history.
- No semester-scoped balance.
- Validation is not enforced by submit in SRA-2.
- No frontend integration, draft generation, or AI.
