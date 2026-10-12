# GTIP hierarchy compatibility experiment

The 0.8.16 reader loaded only `groups.dat` and `regiments.dat`. Its regimental presentation mapping left HQ symbol 18 as Army Group. GTIP uses that persisted symbol for its extra Army HQ; the actual Army Groups remain separate native objects. This explains both the mislabeled Army and the missing Army Groups without indicating a damaged GTIP save.

The native save writer and loader were inspected locally and the mapping was checked against a game-written 1.142 save. No game code or save data is distributed here.

`armygroups.dat` has a record count followed by six lines per record: record ID, name, commander ID, semicolon-delimited attached `groups.dat` IDs (zero-padded by the writer), display override level, and color. Membership comes from these explicit group IDs. `armygrouprefs.dat` instead contains commander assignments and career-mode flags; its integers must not be treated as Army Group parent IDs. Faction is determined from attached commands, or commander faction for an empty Army Group, matching the native lookup. Unknown-faction empty groups cannot be placed on a faction tab.

The editor keeps the native HQ dictionary and Parent_ID fields unchanged. Separate display nodes use negative UI-only identities and aggregate the existing attached root commands. Army Group records and attachments are inspection only; root commands with these attachments cannot be structurally moved. Unsupported or ambiguous attachments fail loading rather than inventing parentage. Missing `armygroups.dat` remains supported for older/partial saves. Its bytes are monitored for external changes and preserved during ordinary HQ/unit edits.

Regimental-scale presentation maps the GTIP Army HQ symbol 18 to Army; the separate record flag always identifies an Army Group. Brigade-scale labels remain unchanged. Enable **Regimental scale** when opening a GTIP regimental campaign.

Verification includes synthetic hierarchy, malformed-reference, no-op save, HQ rename round-trip, byte preservation, and external-change checks. A WPF test renders all six levels and checks inspection-only controls; the existing service/UI regression suites also run. A read-only audit of a local game save resolved three Army Groups and one GTIP Army. In-game acceptance of editor-written changes remains a separate manual check.

Run the focused UI check with `dotnet run --project tests/UiVerification -c Release -- --army-group-ui artifacts/army-group-ui`. The service suite runs the Army Group checks automatically. `tests/Verification --army-group-save <save-directory>` performs a read-only local-save audit (via `dotnet run --project tests/Verification -c Release -- --army-group-save <save-directory>`).

## Creation in test build 2

Army Group creation appends a separate six-field record and updates the commander-assignment section of `armygrouprefs.dat` for selected independent commands. Undo reverses both. Army HQ creation uses native symbol 18 and a normal independent town deployment; this choice is enabled only when the selected installation contains GTIP.Runtime.dll. Existing Army Group membership and commander edits remain inspection-only.

New officer creation is described in [CREATE_OFFICERS](CREATE_OFFICERS.md). Synthetic rollback tests cover new files absent from the original save. An integration test creates two officers, an Army and an Army Group in a disposable copy of a local campaign, then saves/reloads and undoes all creations back to the original file bytes. This is a storage check; loading the resulting formations in game is still required for acceptance.
