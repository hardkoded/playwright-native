/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Official Playwright <c>FrameSession</c> for an out-of-process iframe:
    /// enable Page/Runtime, adopt the frame tree, auto-attach nested OOPIFs
    /// before <c>Runtime.runIfWaitingForDebugger</c>, and forward lifecycle.
    /// </summary>
    internal static class OopifExecution
    {
        private static readonly ConcurrentDictionary<string, OopifState> States = new();

        /// <summary>
        /// Enables Runtime on <paramref name="session"/> and assigns default
        /// contexts to the matching frame.
        /// </summary>
        /// <param name="session">The flattened iframe target session.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="targetId">The CDP target id, which matches the OOPIF frame id.</param>
        internal static void Attach(CRSession session, CRPage page, string targetId)
        {
            if (session == null || page == null)
            {
                return;
            }

            string sessionKey = session.SessionId ?? string.Empty;
            OopifState state = new OopifState(session, page, targetId);
            if (!string.IsNullOrEmpty(sessionKey) && !States.TryAdd(sessionKey, state))
            {
                return;
            }

            session.MessageReceived += state.OnMessage;
            _ = InitializeAsync(state);
        }

        private static async Task InitializeAsync(OopifState state)
        {
            try
            {
                // Official FrameSession applies network, init scripts, and
                // emulation while the target is still paused, then resumes.
                Task autoAttach = state.Session.SendAsync(
                    "Target.setAutoAttach",
                    new { autoAttach = true, waitForDebuggerOnStart = true, flatten = true });
                state.MarkReady();
                await Task.WhenAll(
                    autoAttach,
                    state.Session.SendAsync("Page.enable"),
                    state.Session.SendAsync("Page.setLifecycleEventsEnabled", new { enabled = true }),
                    state.Session.SendAsync("Runtime.enable")).ConfigureAwait(false);
                try
                {
                    await state.Session.SendAsync("Security.setIgnoreCertificateErrors", new { ignore = true }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                await state.Page.ApplyOopifSessionAsync(state.Session, state.TargetId).ConfigureAwait(false);
                Task resume = state.Session.SendAsync("Runtime.runIfWaitingForDebugger");
                Task<JsonElement?> treeTask = state.Session.SendAsync("Page.getFrameTree");
                await Task.WhenAll(resume, treeTask).ConfigureAwait(false);
                _ = state.Page.EnableOopifFetchAsync(state.Session);

                JsonElement? treeResult = await treeTask.ConfigureAwait(false);
                if (treeResult.HasValue
                    && treeResult.Value.TryGetProperty("frameTree", out JsonElement tree))
                {
                    HandleFrameTree(state.Page.FrameManager, tree);
                }
            }
            catch (PlaywrightNativeException)
            {
                // Target may detach before domains are enabled.
            }
        }

        private static void HandleFrameTree(FrameManager frames, JsonElement tree)
        {
            if (frames == null || tree.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (tree.TryGetProperty("frame", out JsonElement frame))
            {
                AdoptFrame(frames, frame);
            }

            if (tree.TryGetProperty("childFrames", out JsonElement children)
                && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    HandleFrameTree(frames, child);
                }
            }
        }

        private static void AdoptFrame(FrameManager frames, JsonElement frame)
        {
            string frameId = ReadString(frame, "id");
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            string parentId = ReadString(frame, "parentId");
            if (!string.IsNullOrEmpty(parentId))
            {
                frames.FrameAttachedToTarget(frameId, parentId);
            }

            Frame existing = frames.FrameById(frameId);
            if (existing == null)
            {
                return;
            }

            string url = ReadString(frame, "url");
            string fragment = ReadString(frame, "urlFragment");
            if (!string.IsNullOrEmpty(fragment))
            {
                url += fragment;
            }

            // Official getFrameTree uses initial=true and must not wipe
            // lifecycle already recorded for this document (setContent / load).
            existing.ApplyFrameTreeSnapshot(
                url,
                ReadString(frame, "name"),
                ReadString(frame, "loaderId"));
        }

        private static void OnContextCreated(
            CRSession session,
            FrameManager frames,
            string targetId,
            JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("context", out JsonElement contextPayload))
            {
                return;
            }

            int contextId = contextPayload.TryGetProperty("id", out JsonElement idElement)
                && idElement.TryGetInt32(out int parsed)
                ? parsed
                : 0;
            if (contextId == 0)
            {
                return;
            }

            bool isDefault = true;
            string frameId = targetId ?? string.Empty;
            if (contextPayload.TryGetProperty("auxData", out JsonElement auxData))
            {
                if (auxData.TryGetProperty("isDefault", out JsonElement isDefaultElement)
                    && isDefaultElement.ValueKind == JsonValueKind.False)
                {
                    isDefault = false;
                }

                if (auxData.TryGetProperty("frameId", out JsonElement frameIdElement)
                    && frameIdElement.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(frameIdElement.GetString()))
                {
                    frameId = frameIdElement.GetString();
                }
            }

            if (!isDefault)
            {
                return;
            }

            frames.RememberDefaultContext(frameId, new CRExecutionContext(session, contextId));
        }

        private static void OnFrameAttached(CRPage page, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            page?.HandleFrameAttached(
                ReadString(parameters.Value, "frameId"),
                ReadString(parameters.Value, "parentFrameId"));
        }

        private static void OnFrameNavigated(FrameManager frames, JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("frame", out JsonElement framePayload))
            {
                return;
            }

            string frameId = ReadString(framePayload, "id");
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            string parentId = ReadString(framePayload, "parentId");
            if (!string.IsNullOrEmpty(parentId))
            {
                frames.FrameAttachedToTarget(frameId, parentId);
            }

            string url = ReadString(framePayload, "url");
            string fragment = ReadString(framePayload, "urlFragment");
            if (!string.IsNullOrEmpty(fragment))
            {
                url += fragment;
            }

            frames.FrameCommittedNewDocumentNavigation(
                frameId,
                url,
                ReadString(framePayload, "name"),
                ReadString(framePayload, "loaderId"));
        }

        private static void OnFrameDetached(CRPage page, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            page?.HandleFrameDetached(
                ReadString(parameters.Value, "frameId"),
                ReadString(parameters.Value, "reason"));
        }

        private static void OnLifecycleEvent(CRPage page, FrameManager frames, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string name = ReadString(parameters.Value, "name");
            string frameId = ReadString(parameters.Value, "frameId");
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(frameId))
            {
                frames.FrameLifecycleEvent(frameId, name);
                if (string.Equals(name, "DOMContentLoaded", StringComparison.Ordinal)
                    || string.Equals(name, "load", StringComparison.Ordinal))
                {
                    page?.NetworkManager.FinishNavigationRequestsForFrame(frameId);
                }
            }
        }

        private static void OnAttachedToTarget(OopifState state, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            if (!payload.TryGetProperty("targetInfo", out JsonElement targetInfo)
                || !payload.TryGetProperty("sessionId", out JsonElement sessionEl))
            {
                return;
            }

            string sessionId = sessionEl.GetString();
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            string type = ReadString(targetInfo, "type");
            string targetId = ReadString(targetInfo, "targetId");
            string parentFrameId = ReadString(targetInfo, "parentFrameId");
            CRSession child = state.Session.CreateChildSession(sessionId);
            if (string.Equals(type, "iframe", StringComparison.Ordinal)
                || string.Equals(type, "guest", StringComparison.Ordinal))
            {
                state.Page.AttachOopifSession(child, targetId, parentFrameId);
                return;
            }

            if (string.Equals(type, "worker", StringComparison.Ordinal))
            {
                state.Page.AttachChildWorker(child, sessionId, targetInfo, state.TargetId);
                return;
            }

            _ = child.SendAsync("Runtime.runIfWaitingForDebugger");
        }

        private static string ReadString(JsonElement item, string name)
            => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private sealed class OopifState
        {
            private readonly List<JsonElement> _bufferedAttaches = new();
            private bool _ready;

            internal OopifState(CRSession session, CRPage page, string targetId)
            {
                Session = session;
                Page = page;
                TargetId = targetId;
            }

            internal CRSession Session { get; }

            internal CRPage Page { get; }

            internal string TargetId { get; }

            internal void MarkReady()
            {
                List<JsonElement> pending;
                lock (_bufferedAttaches)
                {
                    _ready = true;
                    pending = new List<JsonElement>(_bufferedAttaches);
                    _bufferedAttaches.Clear();
                }

                foreach (JsonElement attach in pending)
                {
                    OnAttachedToTarget(this, attach);
                }
            }

            internal void OnMessage(string method, JsonElement? parameters)
            {
                FrameManager frames = Page.FrameManager;
                switch (method)
                {
                    case "Runtime.executionContextCreated":
                        OnContextCreated(Session, frames, TargetId, parameters);
                        break;
                    case "Page.frameAttached":
                        OnFrameAttached(Page, parameters);
                        break;
                    case "Page.frameNavigated":
                        OnFrameNavigated(frames, parameters);
                        break;
                    case "Page.frameDetached":
                        OnFrameDetached(Page, parameters);
                        break;
                    case "Page.lifecycleEvent":
                        OnLifecycleEvent(Page, frames, parameters);
                        break;
                    case "Target.attachedToTarget":
                        BufferOrHandleAttach(parameters);
                        break;
                    case "Target.detachedFromTarget":
                        Page.DetachChildTarget(parameters);
                        break;
                    default:
                        Page.HandleOopifProtocolMessage(Session, method, parameters);
                        break;
                }
            }

            private void BufferOrHandleAttach(JsonElement? parameters)
            {
                if (!parameters.HasValue)
                {
                    return;
                }

                lock (_bufferedAttaches)
                {
                    if (!_ready)
                    {
                        _bufferedAttaches.Add(parameters.Value.Clone());
                        return;
                    }
                }

                OnAttachedToTarget(this, parameters);
            }
        }
    }
}
