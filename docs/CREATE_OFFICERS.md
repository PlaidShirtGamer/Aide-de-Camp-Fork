# Create an officer

Open an extracted campaign save, then choose **New Officer…** beside Create Unit. Enter the name, faction, rank, land branch, home state, birth date and date of rank. Set background traits and attributes as desired. The date of rank must be on or before the campaign date, at least sixteen years after birth.

Choose **Choose PNG portrait…** to preview a custom image, or leave the default silhouette. PNGs must be at most 10 MB and 4096×4096 pixels. Select the game's installation first if importing a portrait. Review and create stages an unassigned, active officer in the current save; it does not update the base game's commander catalog. The officer appears in the Officers roster and is available in Create Unit's commander choices immediately. The game still applies its command eligibility rules.

Use **Save changes** to write the record. The editor backs up the save and installs a uniquely named PNG under the selected game's `Avatar` directory. Existing portrait files are never overwritten. A save failure rolls back newly installed portraits along with replaced save files. If you share or move the save, copy the corresponding `adc-*.png` portrait into the destination game's Avatar directory as well. Undo after a successful save retains that harmless image file so another save referencing it does not lose its portrait. Changes require write access to the save and, when importing a portrait, the game's Avatar directory.

Ctrl+Z/Redo treat officer creation as one operation. Undo later unit or Army Group assignments before undoing the officer they use. Ordinary attribute edits continue to use the existing officer editor. Base-game and career-avatar creation are outside this save-creation feature.

## Record mapping and verification

The locally inspected game commander loader/writer uses a count and 66 fields per officer. New IDs append to the contiguous ID range; existing IDs and records are retained. The new record initializes available status, active flag, rank and attained promotion dates, a valid date of birth, selected attributes and traits, no service history, no battles, no scheduled fame/unavailability dates, no portrait/frame index, and the content-addressed portrait basename. Higher promotion dates remain unset. Dummy/internal rank 10 is not offered. Portraits use the game's native `Avatar/<basename>.png` lookup.

If `attributes.dat` exists, creation also appends zeroed change-history arrays (widths 8, 7, 7, 6, 6, 6) and increments its counter. Existing history is preserved. The game initializes these arrays itself when the file is absent. Management edits and record creation share one commander buffer, preventing either operation from overwriting the other during Save.

Synthetic tests cover IDs, immediate availability, edited-new-officer round trips, portrait installation, file-lock rollback, rank/date rejection, undo/redo across saves and Army Group assignment references. A local-copy integration test covers two created officers assigned to a newly created Army and Army Group. WPF tests exercise both creation/review dialogs. Game-load acceptance, portrait appearance and command availability should still be checked in game before relying on a new campaign.
