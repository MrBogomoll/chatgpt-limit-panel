# Quota Panel 1.0.1 window tracking

Keep the panel on a visible inactive ChatGPT window, in the same Z-order band,
and follow rapid window dragging without queuing heavyweight diagnostic work.
Preserve minimize/close/pause behavior, menu layering, settings and input focus.

1. Reproduce inactive-window visibility and movement latency using only isolated
   own offscreen HWND fixtures; never manipulate the user's ChatGPT window.
2. Follow target Z order without a cross-process owner or input attachment.
3. Split immediate target geometry from process discovery, theme reads, preview
   rendering and state writes. Use narrow native hooks and a temporary 16 ms
   movement timer to fill gaps; coalesce slow refresh requests.
4. Verify regression coverage, package from the curated private source, update
   the existing registered local source and reload via the native plugin CLI.
   Preserve preferences and the existing user startup task.
5. Verify installed payload hashes and live lifecycle/real quota delivery.
   Ask the user to check physical dragging; automated HWND tests do not prove
   visual perfection under all desktop compositor loads or monitors.

Evidence so far: movement regression failed the original queued callback on
the first position change. The new direct path passed forty moves before any
dispatcher pump, without diagnostics/PNG writes; the temporary timer followed
a move with no location event and stopped at the end of dragging. The relevant
root suite passed 115/115. A further cross-process WinEvent test raised the
curated release suite to 116/116, with no errors or skips. An incoming quota
update during movement also defers PNG/startup writes and flushes the newest
state at the end. The user confirmed both inactive visibility and normal rapid
dragging after installation. All 22 installed payload hashes match the release;
the installed lifecycle and real quota smoke check passed, preserving English
and the enabled autostart setting.
