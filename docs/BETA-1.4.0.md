# 1.4.0 preview

This branch contains a local preview. The latest public stable release is 1.3.2.

- Vertical taskbar placement searches actual free gaps beyond the midpoint.
- A verified redundant Start11 legacy layout peer no longer masks free space
  measured by the modern taskbar button provider.
- Drag the taskbar indicator along the taskbar to choose a position. Placement
  snaps to a free gap and preserves other widgets, task buttons and the tray.
  Choosing a side in the context menu restores automatic placement on that side.
- The sidebar panel uses a native child of the verified ChatGPT window when the
  native DPI/ownership checks permit it. Button geometry comes from the app's
  accessible web document; unknown geometry temporarily hides the panel.
- Drag the sidebar panel vertically to choose a free slot. A regular click still
  cycles display modes. **Reset position** restores automatic placement.
- Saved positions are relative to the available length, so display scaling and
  resizing do not persist a fixed screen coordinate.

Real-pointer acceptance remains necessary, including leaving the panel bounds
during dragging, releasing outside, ordinary click handling, context menus, and
100% background transparency. The newly updated ChatGPT interface must also be
checked on an installation where that update is available.

Windows 10 has not been validated. See [compatibility investigation](WINDOWS-COMPATIBILITY.md).
