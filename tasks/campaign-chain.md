# Compatibility campaign chain

Unattended Cloud Agents follow **one** chain. Do not wait for a human to retarget the automation.

Read `tasks/todo.md` **Current Phase**. That line names the active playbook. Follow that file. When it is exhausted, open the **Next campaign** file it names and keep going in the **same run**.

## Order

| Status | Playbook | Next |
|--------|----------|------|
| Exhausted | `tasks/leftover-campaign.md` | `tasks/locator-campaign.md` |
| Complete | `tasks/locator-campaign.md` | `tasks/expect-campaign.md` |
| Exhausted | `tasks/expect-campaign.md` | `tasks/screenshot-campaign.md` |
| Exhausted | `tasks/screenshot-campaign.md` | `tasks/pause-campaign.md` |
| Exhausted | `tasks/pause-campaign.md` | `tasks/client-certificates-campaign.md` |
| Exhausted | `tasks/client-certificates-campaign.md` | `tasks/har-update-campaign.md` |
| Exhausted | `tasks/har-update-campaign.md` | `tasks/firefox-campaign.md` |
| Exhausted | `tasks/firefox-campaign.md` | `tasks/expect-options-campaign.md` |
| Exhausted | `tasks/expect-options-campaign.md` | `tasks/firefox-smoke-campaign.md` |
| Exhausted | `tasks/firefox-smoke-campaign.md` | `tasks/tracing-chunks-campaign.md` |
| Exhausted | `tasks/tracing-chunks-campaign.md` | `tasks/unroute-behavior-campaign.md` |
| Exhausted | `tasks/unroute-behavior-campaign.md` | `tasks/tracing-groups-campaign.md` |
| Exhausted | `tasks/tracing-groups-campaign.md` | `tasks/getby-regex-campaign.md` |
| Exhausted | `tasks/getby-regex-campaign.md` | `tasks/ignore-default-args-campaign.md` |
| Exhausted | `tasks/ignore-default-args-campaign.md` | `tasks/firefox-persistent-campaign.md` |
| Exhausted | `tasks/firefox-persistent-campaign.md` | `tasks/locator-leftover-campaign.md` |
| Exhausted | `tasks/locator-leftover-campaign.md` | `tasks/v160-leftover-campaign.md` |
| Exhausted | `tasks/v160-leftover-campaign.md` | `tasks/locator-filter-campaign.md` |
| Exhausted | `tasks/locator-filter-campaign.md` | `tasks/aria-snapshot-leftover-campaign.md` |
| Exhausted | `tasks/aria-snapshot-leftover-campaign.md` | `tasks/frame-locator-leftover-campaign.md` |
| Exhausted | `tasks/frame-locator-leftover-campaign.md` | `tasks/aria-snapshot-json-campaign.md` |
| Exhausted | `tasks/aria-snapshot-json-campaign.md` | `tasks/click-steps-campaign.md` |
| Exhausted | `tasks/click-steps-campaign.md` | `tasks/console-filter-campaign.md` |
| Exhausted | `tasks/console-filter-campaign.md` | `tasks/dblclick-steps-campaign.md` |
| Exhausted | `tasks/dblclick-steps-campaign.md` | `tasks/web-storage-campaign.md` |
| Exhausted | `tasks/web-storage-campaign.md` | `tasks/api-response-leftover-campaign.md` |
| Exhausted | `tasks/api-response-leftover-campaign.md` | `tasks/official-leftover-campaign.md` |
| Exhausted | `tasks/official-leftover-campaign.md` | `tasks/tracing-har-campaign.md` |
| Exhausted | `tasks/tracing-har-campaign.md` | `tasks/screencast-campaign.md` |
| Exhausted | `tasks/screencast-campaign.md` | `tasks/selectors-campaign.md` |
| Exhausted | `tasks/selectors-campaign.md` | `tasks/credentials-campaign.md` |
| Exhausted | `tasks/credentials-campaign.md` | none — hunt official leftovers |
| Exhausted | leftover hunt (Wave 694) | `tasks/upstream-test-parity-campaign.md` |
| Exhausted | `tasks/upstream-test-parity-campaign.md` | no remaining portable `tests/page` / `tests/library` Chromium/WebKit titles |

Do **not** invent APIs to pad a campaign. Only official Playwright (Node and/or `microsoft/playwright-dotnet`) that this repo still lacks, and that can run on the **direct** Chromium and WebKit stacks (Firefox persistent work is `tasks/firefox-persistent-campaign.md`).

## Handoff (when a playbook is exhausted)

1. In `tasks/todo.md`, move the exhausted campaign under **Previous** and set Current Phase to the first wave of the next playbook (next unused Wave NNN).
2. Point Current Phase at that playbook (`Expect campaign: follow …` becomes `Screenshot campaign: follow …`, etc.).
3. Add the first wave row to the next playbook's table if it is missing.
4. Continue the per-wave loop. Do **not** stop. Do **not** wait for another prompt.

When `tasks/credentials-campaign.md` is exhausted: hunt another official leftover. Do **not** invent filler APIs.

When leftover hunt is exhausted: follow `tasks/upstream-test-parity-campaign.md` (port official Node tests; fix the library when a faithful port fails).

## Automation prompt (for cursor.com/automations)

Use a **schedule** (every 2–4 hours). Do **not** trigger on push to `main` (that cancels an in-flight run).

```
Continue the PlaywrightNative compatibility campaigns on hardkoded/playwright-native.

Read tasks/campaign-chain.md and tasks/todo.md Current Phase. Follow the playbook named there.

When a playbook says it is exhausted, open the Next campaign file it names, write the first Current Phase wave, and keep going in the same run.

Start from latest origin/main. Create cursor/<descriptive-name>-554a. Implement the next 50 waves of the active campaign (follow the chain if one campaign ends mid-run).

After each wave: Chromium + WebKit tests green, feat commit, docs commit, fast-forward merge to main, push origin/main. Do not open pull requests.

Do not invent fake APIs. Do not start wave N+1 until wave N is on origin/main. If another cursor/*-554a branch was pushed in the last 6 hours and is unmerged, stop.
```
