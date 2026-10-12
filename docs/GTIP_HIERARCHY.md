# GTIP hierarchy compatibility experiment

The 0.8.16 reader loaded only `groups.dat` and `regiments.dat`. Its regimental presentation mapping left HQ symbol 18 as Army Group. GTIP uses that persisted symbol for its extra Army HQ; the actual Army Groups remain separate native objects. This explains both the mislabeled Army and the missing Army Groups without indicating a damaged GTIP save.

The native save writer and loader were inspected locally and the mapping was checked against a game-written 1.142 save. No game code or save data is distributed here.

`armygroups.dat` has a record count followed by six lines per record: record ID, name, commander ID, semicolon-delimited attached `groups.dat` IDs (zero-padded by the writer), display override level, and color. Membership comes from these explicit group IDs. `armygrouprefs.dat` instead contains commander assignments and career-mode flags; its integers must not be treated as Army Group parent IDs. Faction is determined from attached commands, or commander faction for an empty Army Group, matching the native lookup. Unknown-faction empty groups cannot be placed on a faction tab.

The editor keeps the native HQ dictionary and Parent_ID fields unchanged. Separate display nodes use negative UI-only identities and aggregate the existing attached root commands. Army Group names and commanders remain inspection only. Root land commands can transfer between Army Groups using the dedicated membership operation described below; they cannot become ordinary subordinate HQs while attached. Unsupported or ambiguous attachments fail loading rather than inventing parentage. Missing `armygroups.dat` remains supported for older/partial saves. Its bytes are monitored for external changes and preserved during ordinary HQ/unit edits.

Regimental-scale presentation maps the GTIP Army HQ symbol 18 to Army; the separate record flag always identifies an Army Group. Brigade-scale labels remain unchanged. Enable **Regimental scale** when opening a GTIP regimental campaign.

Verification includes synthetic hierarchy, malformed-reference, no-op save, HQ rename round-trip, byte preservation, and external-change checks. A WPF test renders all six levels and checks inspection-only controls; the existing service/UI regression suites also run. A read-only audit of a local game save resolved three Army Groups and one GTIP Army. In-game acceptance of editor-written changes remains a separate manual check.

Run the focused UI check with `dotnet run --project tests/UiVerification -c Release -- --army-group-ui artifacts/army-group-ui`. The service suite runs the Army Group checks automatically. `tests/Verification --army-group-save <save-directory>` performs a read-only local-save audit (via `dotnet run --project tests/Verification -c Release -- --army-group-save <save-directory>`).

## Creation in test build 2

Army Group creation appends a separate six-field record and updates the commander-assignment section of `armygrouprefs.dat` for selected independent commands. Undo reverses both. Army HQ creation uses native symbol 18 and a normal independent town deployment; this choice is enabled only when the selected installation contains GTIP.Runtime.dll. Membership transfers are supported as of 0.8.19; Army Group commander edits remain inspection-only.

New officer creation is described in [CREATE_OFFICERS](CREATE_OFFICERS.md). Synthetic rollback tests cover new files absent from the original save. An integration test creates two officers, an Army and an Army Group in a disposable copy of a local campaign, then saves/reloads and undoes all creations back to the original file bytes. This is a storage check; loading the resulting formations in game is still required for acceptance.

## Commander eligibility in 0.8.17

Creation reads the 66-field `commanders.txt` records from current staged data: faction at offset 4, raw experience at 5 (0–100), land branch at 13 (-1 through 3), unavailability day/month/year at 50–52, rank at 59 (1–9), status at 60 (0 available), and active flag at 65. All-nonpositive unavailability dates are unset. Assignments are collected across combat units, HQs and separate Army Groups.

Minimum ranks come from the four 19-entry sections of `ranks.txt`, with `ranks0.txt`/`ranks1.txt` overrides in the same directory. Apply the configured campaign hierarchy shift; native 18 shifted to 17 uses land tier 16. Completed project 90 (unshifted) or 89 (regimental shift -1) selects post-reform ranks. The existing mapped nation progression reader includes staged project edits. If reform cannot be resolved and the pre/post minima differ, creation is blocked with an explanation.

Army Group minimum experience comes from `dlcwl_config.dat`, converted from 0–1 to the officer's saved 0–100 scale. Inspection of the game's suitability logic confirms it tests raw experience only at the minimum rank. GTIP waives that experience check for actual Army HQs. The editor enforces minimum ranks for all new formations as a creation constraint; it does not implement every game-specific promotion or reassignment rule.

The dropdown snapshots requirements on opening (no disk reads while changing filters); final validation reloads them and rechecks staged officer availability. Synthetic tests cover rank/experience boundaries, faction overrides, missing reform, malformed configuration, effective dates, status, branch, assignments and actual WPF tier/faction selection behavior. Save-copy round trips verify storage without altering the source save; in-game acceptance remains a separate check.

## Army Group transfers in 0.8.19

In Tree view, drag an Army or other independent land HQ onto the desired Army Group's card. It must belong to the same faction. The whole command transfers; its own commander and subordinate hierarchy remain unchanged. Ctrl+Z/Redo includes membership, and Save changes uses the usual backup and conflict checks. Army Groups themselves, individual regiments, and nonroot HQs cannot be moved this way. Army Group names and commanders remain inspection-only.

The transfer updates only the affected semicolon-separated HQ lists in six-field `armygroups.dat` records and the moved root HQ's commander reference in `armygrouprefs.dat` (indexed by native HQ record order). Native `groups.dat` parent IDs remain unchanged. Synthetic tests verify save/reload, byte-identical undo after saving, unchanged HQ records, faction/root restrictions, and the WPF preview/drop handlers. In-game acceptance remains a separate check.
