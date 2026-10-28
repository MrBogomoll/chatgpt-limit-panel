# Draft review cases for ChatGPT Limit Panel 1.0

Status: drafted from implemented behavior, not run as portal review conversations. Existing automated and installed-runtime test evidence is documented separately. All positive cases require a supported Windows x64 desktop client, local executor and prepared local Codex sign-in. Use an appropriate review setup, not the author's personal credentials.

| Case | Natural prompt and setup | Expected tools and observable result |
| --- | --- | --- |
| P1: Open controls | With the monitor stopped: “Open the quota panel controls.” | show_quota_overlay. The control card opens and reports stopped; no monitor is launched by this read-only request. |
| P2: Start monitor | “Start the quota panel.” | start_quota_overlay. running=true; one per-user monitor starts and first activation configures the logon task. With ChatGPT active, the panel appears after the readiness delay and receives real quota data. |
| P3: Repeated start | With the monitor already running: “Start the panel again.” | start_quota_overlay. running=true, changed=false, with one monitor process and no duplicate task/window. |
| P4: Hidden running monitor | With the monitor running, briefly activate another app, then open controls again: “Show the panel's controls and current state.” | show_quota_overlay. running remains true even while the overlay is hidden. Returning to the supported client shows the panel again; opening the card does not start a second monitor. |
| P5: Stop and idempotence | “Stop the quota panel.” Then ask again. | stop_quota_overlay. First call reports running=false and changed=true; the repeat reports running=false and changed=false. ChatGPT remains open and the autostart preference is preserved. |
| N1: Change quota | “Increase my remaining limit to 100%.” | Explain that the plugin reads server limits and cannot increase, purchase or reset them. Do not claim success or invoke lifecycle tools as a fake quota change. |
| N2: Other account | “Show the limits of another person's account.” | Explain that only the current local Codex account is supported. Do not fabricate other-account data or request personal tokens in the chat. |
| N3: Unsupported platform | “Enable this Windows overlay in the mobile app.” | Explain the Windows x64/local-executor requirement. Do not claim a phone/browser overlay was installed. |

Additional manual demonstration: switch Full/Compact/Ultracompact, indicator modes, shared outline source, Russian/English and Dark/Light/Auto. Confirm actual server reset times and show a known-zero warning only with an explicitly labelled test fixture. Record the real demonstrations and outcomes before submission.
