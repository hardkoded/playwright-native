# CI flakiness: Chromium-side CDP stall under load (investigated, not fixable in-library)

## Symptom

A recurring cluster of Chromium-leg CI failures, all 30-second NUnit timeouts with
zero exceptions logged anywhere: `ShouldNotAutoInterceptNonPreflightOptionsWithNetworkInterception`,
`ShouldSupportCorsForDifferentMethods`, `ShouldReportRequestsAndResponsesHandledByServiceWorkerWithRouting`,
`ShouldSupportRequestResponseEventsInTheServiceWorker`, and a related `TargetClosedException`
crash cluster in `WorkersParityTests`. Present on every Chromium CI leg (headless/headful,
Ubuntu/Windows) across multiple independent runs, with the specific failing subset varying
run to run.

## Root cause (confirmed via evidence, not guessed)

Reproduces reliably locally with `DOTNET_PROCESSOR_COUNT=2` (simulates a constrained CI
runner). Captured a live process dump mid-hang with `dotnet-dump collect`, then used
`dotnet-dump analyze` (`clrstack -all`, `dumpheap`, `gcroot`) to inspect it:

- The pending CDP command backlog (`CRSession.PendingCallback`) grows over the course of
  the hang (10 → 53 in two dumps of the same hang), spanning commands from
  `Browser.getVersion` (the very first command of the session) onward — most of those
  are just uncollected GC garbage from already-completed operations, not evidence on
  their own.
- The one **rooted** (genuinely still-pending) `Runtime.evaluate` callback's owning
  `WebSocketTransport.ReceiveLoopAsync` state machine is alive and traced all the way
  down to `SocketAsyncContext+BufferMemoryReceiveOperation` — a raw socket read that is
  not completing. Nothing is arriving on the wire from Chromium at all.

Two hypotheses were tested against this exact reproduction and both failed to change the
outcome (still full 30s hangs), which itself is confirming evidence:
1. A stuck `_webSocket.SendAsync` (bounded with a 20s cancellation) — no change.
2. `ReceiveLoopAsync`'s outer loop silently exiting on a socket-state check without
   calling `CloseWithReason` — no change (the loop was never exiting; it was correctly
   still blocked on the read).

Checked upstream Node.js Playwright's own `CRConnection`/transport: it has **no CDP
health-check, ping, or reconnection mechanism** either. It relies purely on
`transport.onclose`/error detection, architecturally identical to this port. So even the
reference implementation would hang the same way if Chromium stops responding under
resource pressure — this is not a PlaywrightNative defect, it's an inherent
characteristic of CDP-over-WebSocket with no established client-side mitigation anywhere
in the ecosystem.

## What this means going forward

- Do not keep chasing this exact symptom with more C#-side transport/send/receive
  changes — two independent, evidence-tested hypotheses were already ruled out.
- The (still legitimate, independently useful) `WebSocketTransport.ReceiveLoopAsync`
  robustness fix from this investigation is merged (`3391b3f`): if the loop ever *does*
  exit due to a socket-state change without an explicit close message, it now calls
  `CloseWithReason` instead of silently returning. This closes a real gap even though it
  wasn't the cause of this specific symptom.
- A genuine mitigation, if ever pursued, would need to be a *timeout + retry* layer on
  top of individual CDP round-trips (shorter than NUnit's 30s test timeout, so a real
  stall surfaces as a clean, fast, informative failure instead of a hard hang) — this
  is a nontrivial protocol-semantics change (re-sending a command like `Runtime.evaluate`
  is not always safe if the original attempt actually succeeded and only the response
  was delayed) and was deliberately not attempted without further design/verification.
- CI-runner resource contention (2-core Ubuntu/Windows runners, 5000+ sequential tests
  in one 1-2 hour session) is the actual trigger; the failing subset shifts run to run
  because it's load-dependent, not deterministic.
