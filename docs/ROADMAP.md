# Next release: 1.1.0

User request recorded 2026-10-25 while finishing 1.0.1:

Add a manual **Check for updates / Проверить обновления** action. Check the
latest published release of MrBogomoll/quota-panel-releases, compare its
semantic version with the installed version, and report whether an update is
available. Offer the verified GitHub release/download page when newer.

Exclude drafts and prereleases from the stable update check. Handle unavailable
network/API responses distinctly from an up-to-date result. Translate results
to the selected interface language. Automatic installation and task migration
are separate scope and are not part of the currently requested button.

This request is intentionally deferred; 1.0.1 contains only the window tracking
and focus visibility fixes. No update-check code is included in 1.0.1.
