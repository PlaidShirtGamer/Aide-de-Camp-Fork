# Aide-de-Camp 0.8.17

- Create Unit lists only active, unassigned land officers in the chosen faction who meet the selected command's minimum rank. Effective unavailability dates are honored.
- Commander entries display proper rank names. Army Group entries also show experience; the game's configured threshold applies at the minimum rank, with higher ranks exempt. GTIP Armies do not inherit that threshold.
- Requirements follow faction rank tables, the campaign hierarchy shift, and completed organization reform. Missing or ambiguous settings show an explanation instead of offering ineligible officers.
- Changing tier or faction immediately updates the list, retaining a selected commander only while eligible. Final creation validation repeats the same checks against current staged data.
- Executable metadata, window title and Windows package are versioned 0.8.17. Includes the preceding test-branch hierarchy, command creation and officer creation features.

## GTIP creation test build 2 (test branch)

- Create Unit now includes Army (when the selected game has GTIP installed) and Army Group. Army Groups can attach eligible independent commands, or start empty without a map icon.
- New Officer creates an available, unassigned officer in the loaded save with faction, rank, dates, land branch, home state, traits and attributes.
- Choose a custom PNG portrait with a preview. Save changes imports a uniquely named copy into the selected game's Avatar folder without overwriting existing portraits.
- New officers appear in the officer roster and command creation choices immediately. Officer and Army Group creation support undo/redo, including across a Save boundary, and use the existing full-save backup and conflict checks.
- Current scope is creating officers in saves. Base-game commander catalogs are not changed. Existing Army Group membership editing remains inspection-only; creation can assign currently independent commands.

## GTIP hierarchy test build (test branch)

- Reads separate Army Group records and attachments from `armygroups.dat`, showing them above their attached commands in Tree and Roster.
- With **Regimental scale** enabled, HQ symbol 18 displays as **Army** with a four-cross counter. Separate Army Group records retain five-cross counters.
- Army Group names, commanders and membership are inspection only. Edit attached formations normally; change Army Group membership in game. Attached root HQs cannot be dragged into other commands while membership records are read-only.
- Loading now monitors `armygroups.dat` for external changes. Display parentage is never serialized into `groups.dat`.

# Aide-de-Camp 0.8.16 — Unit Creation Test Build

Alpha prerelease for Windows x64. Extract `Aide-de-Camp-0.8.16-win-x64.zip` and run
`Aide-de-Camp.exe`; the .NET runtime is included. Start with a copied campaign.

## Included

- Create land HQs and infantry, cavalry or artillery from toolbar, tree and roster context menus, including additions to existing fort garrisons.
- Movable, resizable creation window with advanced choices, explicit parent selection, faction defaults, displayed/native tiers and available name suggestions.
- Commander assignment, home state, volunteer/draft recruitment, contract, size, compatible weapon, training, combat experience, separate HQ/combat perks and perk progress.
- Searchable verified towns and state filtering for independent HQs; percentage or raw starting supply values for combat units.
- Review and confirmation, one-step undo/redo, transactional save with backup, and navigation to the new item.
- Roster selection/editing stability, collapse controls, performance improvements, and the clearer Mass Rename / Naming Schemes label.

## Verification and limits

- 178 service baseline checks, 36 creation writer checks and 18 creation UI checks passed; 40 checks audited the game-written combined test save.
- User confirmed the Union creations in game. Experience, training, assigned perks, perk progress, contracts, equipment and requested stock survived the resave.
- Confederate creations and Richmond placement survived, but the game changed commanders and selected an available infantry perk. Fog of war prevented Confederate visual verification.
- Creation supports save version **1.142**. Mapped but untested combinations require confirmation; malformed data, unresolved identities and unknown layouts remain unavailable. Verified town mapping covers 2 March 1861 through 19 June 1863.
- The broad WPF regression run was interrupted by Windows clipboard access failure; it is not reported as a full pass.
- New forts, Navy creation, new commanders, templates and the Army Builder interface are future work.

See [Create tool mapping](CREATE_TOOL.md), [game test evidence](CREATE_TOOL_GAME_TEST.md)
and [user guide](USER_GUIDE.md). The downloadable checksum accompanies the ZIP.

# Aide-de-Camp 0.8.15 alpha

Fixed the States roster after population and volunteer-target edits. Available volunteers and remaining deficits now use the same estimate as Edit Selected, including partial deficit reductions. Saved counters remain visible for comparison. Undo and redo restore the estimates; unavailable inputs are explicitly marked. Only population is written; the game recalculates the final recruitment pool.

# Aide-de-Camp 0.8.14 alpha

The portable release presents a single executable and README in its root, with documentation and license notices in folders. The executable includes the .NET runtime.

Edit Selected exposes the full combat-unit field set in separate type groups, filters weapon choices, and previews strength maxima and artillery guns. A single commit covers every group, with selected/changed counts and a detailed tooltip. Management editors include fields available to subsets of the selection. Roster cells support validated scalar and rectangular copy/paste with one undo step.

A persistent strip identifies the campaign, save, in-game date and player faction. The save browser uses actual campaign/save labels and displays campaign dates and modification times.

# Aide-de-Camp 0.8.13 alpha

Search and Edit Selected now occupy the same left-hand toolbar positions across Army, Garrison, Navy, Officer, Weapon, and Nation views. Batch Edit remains beside them where supported. Reselect batch restores the previous batch selection and now explains that it does not undo changes. Batch dialog fields select and highlight themselves when changed, then clear when restored. Project and policy cards show their in-game descriptions directly.

# Aide-de-Camp 0.8.12 alpha

Save loading prepares compact and detailed Tree cards for both factions. Zoom reuses those controls and cached measurements instead of rebuilding the entire tree. Individual edits refresh changed cards and ancestor summaries; unchanged metric rows remain alive. Previous measurements remain available for undo/redo. Loading another save or closing clears the cache.

A 331-card synthetic tree measured 40–70 ms transitions after preparation, compared with roughly 1–3 seconds in 0.8.11 on the same machine. Loading does more preparation and retains more UI data in memory; font/card settings changes deliberately invalidate their geometry. See [cache and performance validation](TREE_CACHE_VALIDATION.md).

# Aide-de-Camp 0.8.11 alpha

Tree view uses actual card widths and additive edge gaps, with compact defaults and larger text. Compact HQ and combat cards retain parent command, manpower, casualties, and meaningful alerts; HQ alerts include subordinates. Supplemental details expand only at the configured zoom threshold and release their space when hidden. Nudge offsets and hierarchy undo/redo remain intact, and font/detail reflow preserves a nearby card's screen position.

Existing preferences remain intact: reset the card, spacing, and zoom sections to adopt the new defaults. See [Tree validation and before/after images](TREE_VALIDATION.md) for measured results and boundaries.

# Aide-de-Camp 0.8.10 alpha

The editor now uses the Aide-de-Camp name and ships as a Windows x64 ZIP with its .NET runtime included. Extract the entire ZIP and run Aide-de-Camp.exe. No separate .NET installation is required.

This first dedicated Aide-de-Camp release includes army and garrison editing, ships and readiness controls, officer attributes and fame, weapon management, retained batch selections, and Nation views for treasury, eligible recruitment states, projects, subsidies, and policies. Existing preferences from GTCW.OOBEditor are read as a fallback.

The repository includes MIT-licensed source, retained starter history, a development history, architecture notes, build instructions, and automated verification. Runtime license notices are included in the downloadable package.

This is an alpha: automated checks cover save handling, WPF interaction, and packaged startup; acceptance testing of edited effects inside a running campaign remains outstanding. Use a copied campaign. See the repository's known-limits document for mapping and progression restrictions.
