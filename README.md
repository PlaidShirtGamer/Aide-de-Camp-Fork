# Aide-de-Camp

A Windows save editor for **Grand Tactician: The Civil War (1861–1865)**. Edit armies, garrisons, ships, officers, weapons, and national resources through separate Union and Confederate views.

## Download and run

Version **0.8.20** is available from this fork's [test-branch Actions builds](https://github.com/PlaidShirtGamer/Aide-de-Camp-Fork/actions/workflows/build.yml?query=branch%3Atest). Download the Windows package artifact, extract `Aide-de-Camp-0.8.20-win-x64.zip`, and open **Aide-de-Camp.exe**. The .NET runtime is included; no separate installation is required. GitHub's source archives are for developers, not the runnable app.

Requires Windows 10/11 x64 and your own game installation. Version **0.8.20** is an experimental test-branch build. Creation supports save version **1.142**. Combined in-game tests confirmed specific Union formations, perks and supplies; this does not establish every possible combination. Start with a copied campaign and keep your original saves. See [creation test results](docs/CREATE_TOOL_GAME_TEST.md) and [release notes](docs/RELEASE_NOTES.md).

## Features

- Create land HQs and infantry, cavalry or artillery, including combat units under existing fort commands; review choices, undo the complete creation and save with a backup.
- Searchable verified town placement for independent HQs, filtered equipment, training, experience, separate HQ/combat perks and configurable starting stocks.
- Army and garrison rosters, formation cards, naming rules, and retained batch selections.
- Ships in deployed fleets or port, including construction, repair, and condition editing.
- Officers with experience, fame, attributes, traits, branch, and promotion dates.
- Weapon stockpiles editable per nation, but standardization and order editing need work.
- Nation views for eligible recruitment states, treasury, projects with stage boxes, subsidy funding, and policy progress.
- Full-field selection editing, with separate infantry/cavalry/artillery groups, filtered weapons, strength/gun previews, and one undoable commit.
- Cell copy/paste across roster selections, plus persistent campaign/save/date/player information.
- Compact Tree cards with readable command/strength/alerts, responsive edge gaps, and preserved Nudge offsets.
- Adjustable UI settings and application error logs.


Source code is available under the [MIT license](LICENSE). This is an unofficial independent project. Game assets, campaign saves, and game binaries are not included.
