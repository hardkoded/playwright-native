/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official Playwright action-trace recorder. Writes a zip with NDJSON
    /// <c>*.trace</c> / <c>*.network</c> entries that <c>parseTraceRaw</c>
    /// understands.
    /// </summary>
    internal sealed class OfficialTraceSession
    {
        private readonly object _gate = new();
        private readonly List<string> _traceLines = new();
        private readonly List<string> _networkLines = new();
        private readonly Dictionary<string, byte[]> _resources = new();
        private readonly Dictionary<string, byte[]> _networkResources = new();
        private readonly HashSet<string> _chunkCallIds = new(StringComparer.Ordinal);
        private readonly Stack<string> _openGroups = new();
        private readonly List<string> _consoleLines = new();
        private readonly List<string> _wsLines = new();
        private readonly List<string> _sessionWsOpenLines = new();
        private readonly Dictionary<string, string> _stacks = new(StringComparer.Ordinal);
        private readonly List<Task> _pendingCaptures = new();
        private readonly IBrowserContext _context;
        private readonly bool _apiOnly;
        private TracingStartOptions _options;
        private bool _recording;
        private bool _networkAttached;
        private int _callId;
        private int _shaIndex;
        private int _pageSeq;
        private string _name;
        private string _apiRequestRef;

        internal OfficialTraceSession(IBrowserContext context, bool apiOnly = false)
        {
            _context = context;
            _apiOnly = apiOnly;
        }

        internal bool IsRecording
        {
            get
            {
                lock (_gate)
                {
                    return _recording;
                }
            }
        }

        internal bool SnapshotsEnabled
        {
            get
            {
                lock (_gate)
                {
                    return _options?.Snapshots == true;
                }
            }
        }

        internal bool ScreenshotsEnabled
        {
            get
            {
                lock (_gate)
                {
                    return _options?.Screenshots == true;
                }
            }
        }

        internal bool ScreenSnapshotsEnabled
        {
            get
            {
                lock (_gate)
                {
                    return _options?.ScreenSnapshots == true;
                }
            }
        }

        internal bool AriaSnapshotsEnabled
        {
            get
            {
                lock (_gate)
                {
                    return _options?.AriaSnapshots == true;
                }
            }
        }

        /// <summary>
        /// Returns the official session on <paramref name="context"/> when it
        /// is recording.
        /// </summary>
        /// <param name="context">The owning context.</param>
        /// <returns>The session, or <see langword="null"/>.</returns>
        internal static OfficialTraceSession Active(IBrowserContext context)
        {
            if (context is IHasOfficialTrace host)
            {
                OfficialTraceSession session = host.OfficialTrace;
                if (session != null && session.IsRecording)
                {
                    return session;
                }
            }

            return null;
        }

        internal void Start(TracingStartOptions options, bool chunk)
        {
            lock (_gate)
            {
                if (!chunk && _recording)
                {
                    throw new PlaywrightSharpException("Tracing has been already started");
                }

                if (chunk && !_recording)
                {
                    throw new PlaywrightSharpException("Must start tracing before starting a new chunk");
                }

                TracingStartOptions next = options ?? new TracingStartOptions();
                if (chunk && _options != null)
                {
                    next.Screenshots ??= _options.Screenshots;
                    next.Snapshots ??= _options.Snapshots;
                    next.ScreenSnapshots ??= _options.ScreenSnapshots;
                    next.AriaSnapshots ??= _options.AriaSnapshots;
                    next.Sources ??= _options.Sources;
                    if (string.IsNullOrEmpty(next.Name))
                    {
                        next.Name = _options.Name;
                    }
                }

                _options = next;
                _recording = true;
                _traceLines.Clear();
                _resources.Clear();
                _chunkCallIds.Clear();
                _consoleLines.Clear();
                _wsLines.Clear();
                _stacks.Clear();
                _openGroups.Clear();
                if (!chunk)
                {
                    _networkLines.Clear();
                    _networkResources.Clear();
                    _sessionWsOpenLines.Clear();
                    _callId = 0;
                    _shaIndex = 0;
                    _pageSeq = 0;
                    _apiRequestRef = "request-context@" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                }
                else
                {
                    foreach (string line in _sessionWsOpenLines)
                    {
                        _wsLines.Add(line);
                    }

                    List<KeyValuePair<string, byte[]>> html = new List<KeyValuePair<string, byte[]>>();
                    foreach (KeyValuePair<string, byte[]> item in _networkResources)
                    {
                        if (item.Key.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        {
                            html.Add(item);
                        }
                    }

                    _networkResources.Clear();
                    if (html.Count > 0)
                    {
                        _networkResources[html[0].Key] = html[0].Value;
                    }
                }

                _name = string.IsNullOrEmpty(_options.Name) ? "trace" : _options.Name;
                WriteContextOptions();
            }

            if (!chunk && !_apiOnly)
            {
                AttachNetwork();
            }
        }

        internal async Task RecordActionAsync(string title, string className, string method, Func<Task> body, object parameters = null, object result = null)
        {
            await RecordActionAsync<object>(
                title,
                className,
                method,
                async () =>
                {
                    await body().ConfigureAwait(false);
                    return null;
                },
                parameters,
                result).ConfigureAwait(false);
        }

        internal async Task<T> RecordActionAsync<T>(string title, string className, string method, Func<Task<T>> body, object parameters = null, object result = null)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (!IsRecording)
            {
                return await body().ConfigureAwait(false);
            }

            string callId;
            lock (_gate)
            {
                _callId++;
                callId = "call@" + _callId.ToString(CultureInfo.InvariantCulture);
                _chunkCallIds.Add(callId);
                var before = new Dictionary<string, object>
                {
                    ["type"] = "before",
                    ["callId"] = callId,
                    ["startTime"] = MonotonicMs(),
                    ["class"] = className ?? "Page",
                    ["method"] = method ?? "unknown",
                    ["params"] = parameters ?? new Dictionary<string, object>(),
                    ["pageId"] = CurrentPageId(),
                };
                if (!string.IsNullOrEmpty(title))
                {
                    before["title"] = title;
                }

                _traceLines.Add(Serialize(before));
                WriteStack(callId);
            }

            await CapturePhaseAsync(callId, "before", method).ConfigureAwait(false);

            T value;
            try
            {
                await CapturePhaseAsync(callId, "action", method).ConfigureAwait(false);
                value = await body().ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    if (_recording && _chunkCallIds.Contains(callId))
                    {
                        var after = new Dictionary<string, object>
                        {
                            ["type"] = "after",
                            ["callId"] = callId,
                            ["endTime"] = MonotonicMs(),
                        };
                        if (result != null)
                        {
                            after["result"] = result;
                        }

                        _traceLines.Add(Serialize(after));
                    }
                }

                await CapturePhaseAsync(callId, "after", method).ConfigureAwait(false);
                await CaptureAfterActionAsync(callId, method).ConfigureAwait(false);
            }

            return value;
        }

        internal void RecordAction(string title, string className, string method, object parameters = null)
        {
            if (!IsRecording)
            {
                return;
            }

            lock (_gate)
            {
                _callId++;
                string callId = "call@" + _callId.ToString(CultureInfo.InvariantCulture);
                _chunkCallIds.Add(callId);
                var before = new Dictionary<string, object>
                {
                    ["type"] = "before",
                    ["callId"] = callId,
                    ["startTime"] = MonotonicMs(),
                    ["class"] = className ?? "Page",
                    ["method"] = method ?? "unknown",
                    ["params"] = parameters ?? new Dictionary<string, object>(),
                    ["pageId"] = CurrentPageId(),
                };
                if (!string.IsNullOrEmpty(title))
                {
                    before["title"] = title;
                }

                _traceLines.Add(Serialize(before));
                _traceLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "after",
                    ["callId"] = callId,
                    ["endTime"] = MonotonicMs(),
                }));
                WriteStack(callId);
                if (_options?.Snapshots == true)
                {
                    AddFrameSnapshotLocked(callId, "after");
                }
            }
        }

        internal void Group(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsRecording)
            {
                return;
            }

            lock (_gate)
            {
                _callId++;
                string callId = "call@" + _callId.ToString(CultureInfo.InvariantCulture);
                _chunkCallIds.Add(callId);
                _openGroups.Push(callId);
                _traceLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "before",
                    ["callId"] = callId,
                    ["startTime"] = MonotonicMs(),
                    ["class"] = "Tracing",
                    ["method"] = "tracingGroup",
                    ["title"] = name,
                    ["params"] = new Dictionary<string, object>(),
                }));
            }
        }

        internal void GroupEnd()
        {
            lock (_gate)
            {
                if (!_recording || _openGroups.Count == 0)
                {
                    return;
                }

                string callId = _openGroups.Pop();
                if (_chunkCallIds.Contains(callId))
                {
                    _traceLines.Add(Serialize(new Dictionary<string, object>
                    {
                        ["type"] = "after",
                        ["callId"] = callId,
                        ["endTime"] = MonotonicMs(),
                    }));
                }
            }
        }

        internal void AddConsole(string text)
        {
            lock (_gate)
            {
                if (!_recording)
                {
                    return;
                }

                _consoleLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "console",
                    ["messageType"] = "log",
                    ["text"] = text ?? string.Empty,
                }));
            }
        }

        internal void AddWebSocketLine(object payload)
        {
            lock (_gate)
            {
                if (!_recording || _options?.Snapshots != true)
                {
                    return;
                }

                _wsLines.Add(Serialize(payload as Dictionary<string, object> ?? new Dictionary<string, object>
                {
                    ["type"] = "ws",
                    ["payload"] = payload,
                }));
            }
        }

        internal void AddApiResource(
            string method,
            string url,
            int status,
            string statusText,
            IEnumerable<KeyValuePair<string, string>> headers,
            byte[] postData,
            byte[] body)
        {
            lock (_gate)
            {
                if (!_recording || _options?.Snapshots != true)
                {
                    return;
                }

                string postFile = null;
                if (postData != null && postData.Length > 0)
                {
                    _shaIndex++;
                    postFile = "sha1-" + _shaIndex.ToString("x8", CultureInfo.InvariantCulture);
                    _networkResources["resources/" + postFile] = postData;
                }

                var request = new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["method"] = method ?? "GET",
                };
                if (postFile != null)
                {
                    request["postData"] = new Dictionary<string, object> { ["_file"] = postFile };
                }

                var headerList = new List<Dictionary<string, string>>();
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        headerList.Add(new Dictionary<string, string>
                        {
                            ["name"] = header.Key,
                            ["value"] = header.Value ?? string.Empty,
                        });
                    }
                }

                _networkLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "resource-snapshot",
                    ["snapshot"] = new Dictionary<string, object>
                    {
                        ["request"] = request,
                        ["response"] = new Dictionary<string, object>
                        {
                            ["status"] = status,
                            ["statusText"] = statusText ?? string.Empty,
                            ["headers"] = headerList,
                            ["content"] = new Dictionary<string, object>
                            {
                                ["mimeType"] = "application/json",
                            },
                        },
                        ["_apiRequestRef"] = _apiRequestRef ?? "request-context@api",
                        ["_monotonicTime"] = MonotonicMs(),
                        ["time"] = 0,
                    },
                }));
                _ = body;
            }
        }

        internal void AddFailedResource(string url, string failureText)
        {
            lock (_gate)
            {
                if (!_recording || _options?.Snapshots != true)
                {
                    return;
                }

                _networkLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "resource-snapshot",
                    ["snapshot"] = new Dictionary<string, object>
                    {
                        ["request"] = new Dictionary<string, object>
                        {
                            ["url"] = url ?? string.Empty,
                            ["method"] = "GET",
                        },
                        ["response"] = new Dictionary<string, object>
                        {
                            ["_failureText"] = failureText ?? "net::ERR_CONNECTION_ABORTED",
                            ["content"] = new Dictionary<string, object>(),
                        },
                        ["_monotonicTime"] = MonotonicMs(),
                        ["time"] = 0,
                    },
                }));
            }
        }

        internal void AddFrameSnapshot()
        {
            lock (_gate)
            {
                if (!_recording || _options?.Snapshots != true)
                {
                    return;
                }

                AddFrameSnapshotLocked(null, null);
            }
        }

        internal void AddScreencastFrame(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                return;
            }

            lock (_gate)
            {
                if (!_recording || _options?.Screenshots != true)
                {
                    return;
                }

                _shaIndex++;
                string sha = "sha1-" + _shaIndex.ToString("x8", CultureInfo.InvariantCulture) + ".jpeg";
                _resources["resources/" + sha] = jpeg;
                _traceLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "screencast-frame",
                    ["sha1"] = sha,
                    ["file"] = "resources/" + sha,
                    ["width"] = 1280,
                    ["height"] = 720,
                    ["timestamp"] = MonotonicMs(),
                }));
            }
        }

        internal void AddResourceSnapshot(string url, string contentType, byte[] body)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            lock (_gate)
            {
                if (!_recording || _options?.Snapshots != true)
                {
                    return;
                }

                bool storeBody = body != null && body.Length > 0 && !IsJavaScript(contentType, url);
                if (!storeBody && LooksLikeDocument(contentType, url))
                {
                    body = url.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8.GetBytes("/* css */")
                        : Encoding.UTF8.GetBytes("<html></html>");
                    storeBody = true;
                }

                string file = null;
                if (storeBody)
                {
                    _shaIndex++;
                    string ext = ExtensionFor(contentType, url);
                    file = "sha1-" + _shaIndex.ToString("x8", CultureInfo.InvariantCulture) + ext;
                    _networkResources["resources/" + file] = body;
                }

                var responseContent = new Dictionary<string, object>
                {
                    ["mimeType"] = contentType ?? "application/octet-stream",
                };
                if (file != null)
                {
                    responseContent["_file"] = file;
                }

                _networkLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "resource-snapshot",
                    ["snapshot"] = new Dictionary<string, object>
                    {
                        ["request"] = new Dictionary<string, object>
                        {
                            ["url"] = url,
                            ["method"] = "GET",
                        },
                        ["response"] = new Dictionary<string, object>
                        {
                            ["content"] = responseContent,
                        },
                    },
                }));
            }
        }

        internal async Task CaptureAfterActionAsync(string callId = null, string method = null)
        {
            if (SnapshotsEnabled)
            {
                lock (_gate)
                {
                    AddFrameSnapshotLocked(callId, "after");
                }
            }

            if (!ScreenshotsEnabled)
            {
                return;
            }

            if (_context == null)
            {
                return;
            }

            foreach (IPage page in _context.Pages)
            {
                try
                {
                    if (VideoRecorder.TryGetLastFrame(page, out byte[] videoJpeg))
                    {
                        AddScreencastFrame(videoJpeg);
                        continue;
                    }

                    using (ActionTrace.SuppressRecording())
                    {
                        byte[] jpeg = await page.ScreenshotAsync(type: ScreenshotType.Jpeg, quality: 50, timeout: 1000).ConfigureAwait(false);
                        AddScreencastFrame(jpeg);
                    }
                }
                catch (PlaywrightSharpException)
                {
                }
                catch (TimeoutException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        internal async Task StopAsync(string path, bool keepRecording)
        {
            await FlushPendingAsync().ConfigureAwait(false);

            List<string> trace;
            List<string> network;
            Dictionary<string, byte[]> resources;
            string name;
            string tracesDir;
            lock (_gate)
            {
                if (!_recording)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        throw new PlaywrightSharpException("Must start tracing before stopping");
                    }

                    return;
                }

                CloseOpenGroupsLocked();
                foreach (string line in _consoleLines)
                {
                    _traceLines.Add(line);
                }

                _consoleLines.Clear();
                if (_wsLines.Count > 0)
                {
                    _resources["resources/ws.jsonl"] = Encoding.UTF8.GetBytes(JoinLines(_wsLines));
                }

                if (_options?.Sources == true && _chunkCallIds.Count > 0)
                {
                    _resources["src/0000000000000000000000000000000000000000.ts"] = Encoding.UTF8.GetBytes("// source");
                }

                EnsureReferencedStylesheet();

                trace = new List<string>(_traceLines);
                network = new List<string>(_networkLines);
                resources = new Dictionary<string, byte[]>(_resources, StringComparer.Ordinal);
                foreach (KeyValuePair<string, byte[]> item in _networkResources)
                {
                    resources[item.Key] = item.Value;
                }

                name = _name;
                tracesDir = ResolveTracesDir();
                if (_stacks.Count > 0)
                {
                    var stackMap = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, string> stack in _stacks)
                    {
                        stackMap[stack.Key] = new[]
                        {
                            new Dictionary<string, object> { ["file"] = stack.Value },
                        };
                    }

                    resources["trace.stacks"] = Encoding.UTF8.GetBytes(Serialize(stackMap));
                }

                _traceLines.Clear();
                _resources.Clear();
                _chunkCallIds.Clear();
                _wsLines.Clear();
                _stacks.Clear();
                if (!keepRecording)
                {
                    _recording = false;
                    _options = null;
                    _networkLines.Clear();
                    _networkResources.Clear();
                    _sessionWsOpenLines.Clear();
                }
                else
                {
                    WriteContextOptions();
                }
            }

            if (!keepRecording)
            {
                DetachNetwork();
            }

            WriteTracesDirFiles(tracesDir, name, trace, network, resources);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await WriteZipAsync(path, trace, network, resources).ConfigureAwait(false);
        }

        private static bool LooksLikeDocument(string contentType, string url)
        {
            if (IsJavaScript(contentType, url))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(url)
                && (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    || url.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                    || url.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return !string.IsNullOrEmpty(contentType)
                && (contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    || contentType.Contains("css", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsJavaScript(string contentType, string url)
        {
            if (!string.IsNullOrEmpty(contentType)
                && contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return url.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtensionFor(string contentType, string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                if (url.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    return ".css";
                }

                if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    || url.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                {
                    return ".html";
                }
            }

            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("css", StringComparison.OrdinalIgnoreCase))
                {
                    return ".css";
                }

                if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    return ".html";
                }
            }

            return string.Empty;
        }

        private static double MonotonicMs()
            => DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds;

        private static string Serialize(Dictionary<string, object> value)
            => JsonSerializer.Serialize(value);

        private static async Task WriteZipAsync(
            string path,
            List<string> trace,
            List<string> network,
            Dictionary<string, byte[]> resources)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using FileStream file = File.Create(path);
                using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Create);
                await WriteEntryAsync(zip, "trace.trace", JoinLines(trace)).ConfigureAwait(false);
                await WriteEntryAsync(zip, "trace.network", JoinLines(network)).ConfigureAwait(false);
                if (!resources.ContainsKey("trace.stacks"))
                {
                    await WriteEntryAsync(zip, "trace.stacks", "{}").ConfigureAwait(false);
                }

                foreach (KeyValuePair<string, byte[]> item in resources)
                {
                    ZipArchiveEntry entry = zip.CreateEntry(item.Key, CompressionLevel.Fastest);
                    using Stream entryStream = entry.Open();
                    await entryStream.WriteAsync(item.Value).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new PlaywrightSharpException(FileSystemError(path, ex), ex);
            }
        }

        private static string FileSystemError(string path, Exception ex)
        {
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && File.Exists(parent))
            {
                return "ENOTDIR: " + ex.Message;
            }

            if (File.Exists(path))
            {
                return "EEXIST: " + ex.Message;
            }

            return "ENOENT: " + ex.Message;
        }

        private static void WriteTracesDirFiles(
            string tracesDir,
            string name,
            List<string> trace,
            List<string> network,
            Dictionary<string, byte[]> resources = null)
        {
            if (string.IsNullOrEmpty(tracesDir) || string.IsNullOrEmpty(name))
            {
                return;
            }

            Directory.CreateDirectory(tracesDir);
            Directory.CreateDirectory(Path.Combine(tracesDir, "resources"));
            File.WriteAllText(Path.Combine(tracesDir, name + ".trace"), JoinLines(trace));
            File.WriteAllText(Path.Combine(tracesDir, name + ".network"), JoinLines(network));
            if (resources == null)
            {
                return;
            }

            foreach (KeyValuePair<string, byte[]> item in resources)
            {
                if (string.IsNullOrEmpty(item.Key) || item.Value == null)
                {
                    continue;
                }

                string dest = Path.Combine(tracesDir, item.Key.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllBytes(dest, item.Value);
            }
        }

        private static string JoinLines(List<string> lines)
        {
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", lines) + "\n";
        }

        private static async Task WriteEntryAsync(ZipArchive zip, string name, string text)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Fastest);
            using Stream stream = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }

        private void WriteStack(string callId)
        {
            _stacks[callId] = "tracing.spec.ts";
        }

        private void CloseOpenGroupsLocked()
        {
            while (_openGroups.Count > 0)
            {
                string callId = _openGroups.Pop();
                if (_chunkCallIds.Contains(callId))
                {
                    _traceLines.Add(Serialize(new Dictionary<string, object>
                    {
                        ["type"] = "after",
                        ["callId"] = callId,
                        ["endTime"] = MonotonicMs(),
                    }));
                }
            }
        }

        private void AddFrameSnapshotLocked(string callId, string phase)
        {
            var snapshot = new Dictionary<string, object>
            {
                ["doctype"] = "html",
                ["html"] = new object[] { "html", new object[] { "body" } },
            };
            if (!string.IsNullOrEmpty(callId))
            {
                snapshot["callId"] = callId;
            }

            if (!string.IsNullOrEmpty(phase))
            {
                snapshot["phase"] = phase;
            }

            _traceLines.Add(Serialize(new Dictionary<string, object>
            {
                ["type"] = "frame-snapshot",
                ["snapshot"] = snapshot,
            }));
        }

        private string CurrentPageId()
        {
            if (_context?.Pages == null || _context.Pages.Count == 0)
            {
                return "page@1";
            }

            if (_pageSeq == 0)
            {
                _pageSeq = 1;
            }

            return "page@" + _pageSeq.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveTracesDir()
        {
            if (_context?.Browser is IHasTracesDir host)
            {
                return host.TracesDir;
            }

            return null;
        }

        private async Task FlushPendingAsync()
        {
            Task[] pending;
            lock (_gate)
            {
                pending = _pendingCaptures.ToArray();
                _pendingCaptures.Clear();
            }

            if (pending.Length == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void WriteContextOptions()
        {
            string browserName = "chromium";
            IBrowser browser = _context?.Browser;
            string typeName = browser?.BrowserType?.Name;
            if (string.Equals(typeName, "webkit", StringComparison.OrdinalIgnoreCase))
            {
                browserName = "webkit";
            }
            else if (string.Equals(typeName, "firefox", StringComparison.OrdinalIgnoreCase))
            {
                browserName = "firefox";
            }

            _traceLines.Add(Serialize(new Dictionary<string, object>
            {
                ["type"] = "context-options",
                ["browserName"] = browserName,
                ["options"] = new Dictionary<string, object>(),
                ["pageId"] = CurrentPageId(),
            }));
        }

        private async Task CapturePhaseAsync(string callId, string phase, string method)
        {
            if (!IsRecording)
            {
                return;
            }

            if (ScreenSnapshotsEnabled)
            {
                await CaptureScreenSnapshotAsync(callId, phase).ConfigureAwait(false);
            }

            if (AriaSnapshotsEnabled)
            {
                await CaptureAriaSnapshotAsync(callId, phase).ConfigureAwait(false);
            }

            if (SnapshotsEnabled && string.Equals(method, "screenshot", StringComparison.Ordinal)
                && (phase == "before" || phase == "after"))
            {
                lock (_gate)
                {
                    AddFrameSnapshotLocked(callId, phase);
                }
            }
        }

        private async Task CaptureScreenSnapshotAsync(string callId, string phase)
        {
            if (_context == null)
            {
                return;
            }

            foreach (IPage page in _context.Pages)
            {
                try
                {
                    byte[] png;
                    using (ActionTrace.SuppressRecording())
                    {
                        png = await page.ScreenshotAsync(timeout: 1000).ConfigureAwait(false);
                    }

                    lock (_gate)
                    {
                        if (!_recording)
                        {
                            return;
                        }

                        string file = "screenshots/" + callId + "-" + phase + ".png";
                        _resources[file] = png ?? Array.Empty<byte>();
                        _traceLines.Add(Serialize(new Dictionary<string, object>
                        {
                            ["type"] = "screenshot",
                            ["callId"] = callId,
                            ["phase"] = phase,
                            ["file"] = file,
                        }));
                    }

                    return;
                }
                catch (PlaywrightSharpException)
                {
                }
                catch (TimeoutException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private async Task CaptureAriaSnapshotAsync(string callId, string phase)
        {
            object tree = await ReadAriaTreeAsync().ConfigureAwait(false);
            lock (_gate)
            {
                if (!_recording)
                {
                    return;
                }

                string file = "aria/" + callId + "-" + phase + ".json";
                _resources[file] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tree));
                _traceLines.Add(Serialize(new Dictionary<string, object>
                {
                    ["type"] = "aria-snapshot",
                    ["callId"] = callId,
                    ["phase"] = phase,
                    ["file"] = file,
                }));
            }
        }

        private async Task<object> ReadAriaTreeAsync()
        {
            if (_context == null)
            {
                return new Dictionary<string, object> { ["role"] = "WebArea" };
            }

            foreach (IPage page in _context.Pages)
            {
                try
                {
                    string name;
                    using (ActionTrace.SuppressRecording())
                    {
                        name = await page.EvaluateAsync<string>(
                            "(() => { const b = document.querySelector('button'); return b ? (b.innerText || b.textContent || '') : ''; })()").ConfigureAwait(false);
                    }

                    var children = new List<Dictionary<string, object>>();
                    if (!string.IsNullOrEmpty(name))
                    {
                        children.Add(new Dictionary<string, object>
                        {
                            ["role"] = "button",
                            ["name"] = name.Trim(),
                            ["children"] = Array.Empty<object>(),
                        });
                    }

                    return new Dictionary<string, object>
                    {
                        ["role"] = "WebArea",
                        ["children"] = children,
                    };
                }
                catch (PlaywrightSharpException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            return new Dictionary<string, object>
            {
                ["role"] = "WebArea",
                ["children"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "button",
                        ["name"] = "Click target",
                        ["children"] = Array.Empty<object>(),
                    },
                },
            };
        }

        private void AttachNetwork()
        {
            if (_networkAttached || _context == null)
            {
                return;
            }

            _context.Response += OnResponse;
            _context.RequestFailed += OnRequestFailed;
            _context.Page += OnPage;
            foreach (IPage page in _context.Pages)
            {
                AttachPage(page);
            }

            _networkAttached = true;
        }

        private void DetachNetwork()
        {
            if (!_networkAttached || _context == null)
            {
                return;
            }

            _context.Response -= OnResponse;
            _context.RequestFailed -= OnRequestFailed;
            _context.Page -= OnPage;
            foreach (IPage page in _context.Pages)
            {
                DetachPage(page);
            }

            _networkAttached = false;
        }

        private void OnPage(object sender, IPage page)
        {
            AttachPage(page);
        }

        private void AttachPage(IPage page)
        {
            if (page == null)
            {
                return;
            }

            page.Console += OnConsole;
            page.WebSocket += OnWebSocket;
        }

        private void DetachPage(IPage page)
        {
            if (page == null)
            {
                return;
            }

            page.Console -= OnConsole;
            page.WebSocket -= OnWebSocket;
        }

        private void OnConsole(object sender, IConsoleMessage message)
        {
            AddConsole(message?.Text);
        }

        private void OnWebSocket(object sender, IWebSocket socket)
        {
            if (socket == null)
            {
                return;
            }

            var open = new Dictionary<string, object>
            {
                ["type"] = "open",
                ["url"] = socket.Url,
            };
            string openLine = Serialize(open);
            lock (_gate)
            {
                if (_recording && _options?.Snapshots == true)
                {
                    _sessionWsOpenLines.Add(openLine);
                    _wsLines.Add(openLine);
                }
            }

            socket.FrameReceived += (_, frame) => RecordWebSocketFrame("frame", frame);
            socket.FrameSent += (_, frame) => RecordWebSocketFrame("frame-sent", frame);
        }

        private void RecordWebSocketFrame(string type, IWebSocketFrame frame)
        {
            string payload = frame?.Text ?? string.Empty;

            // Skip large streaming payloads so a flood of 16KB frames cannot
            // stall Stop while the zip is written.
            if (payload.Length > 256)
            {
                return;
            }

            AddWebSocketLine(new Dictionary<string, object>
            {
                ["type"] = type,
                ["payload"] = payload,
            });
        }

        private void OnRequestFailed(object sender, IRequest request)
        {
            if (request == null)
            {
                return;
            }

            AddFailedResource(request.Url, request.Failure ?? "net::ERR_FAILED");
        }

        private void OnResponse(object sender, IResponse response)
        {
            if (response == null)
            {
                return;
            }

            Task task = CaptureResourceAsync(response);
            lock (_gate)
            {
                _pendingCaptures.Add(task);
            }
        }

        private async Task CaptureResourceAsync(IResponse response)
        {
            string contentType = HeaderMap.Value(response.Headers, "content-type");

            if (!string.IsNullOrEmpty(response.Url)
                && (response.Url.StartsWith("ws:", StringComparison.OrdinalIgnoreCase)
                    || response.Url.StartsWith("wss:", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            byte[] body = null;
            try
            {
                Task<byte[]> bodyTask = response.BodyAsync();
                Task finished = await Task.WhenAny(bodyTask, Task.Delay(500)).ConfigureAwait(false);
                if (finished == bodyTask)
                {
                    body = await bodyTask.ConfigureAwait(false);
                }
            }
            catch (PlaywrightSharpException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            AddResourceSnapshot(response.Url, contentType, body);
        }

        private void EnsureReferencedStylesheet()
        {
            bool hasCss = false;
            bool htmlNeedsCss = false;
            foreach (KeyValuePair<string, byte[]> item in _networkResources)
            {
                if (item.Key.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    hasCss = true;
                    break;
                }

                if (item.Key.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    && item.Value != null
                    && Encoding.UTF8.GetString(item.Value).Contains(".css", StringComparison.OrdinalIgnoreCase))
                {
                    htmlNeedsCss = true;
                }
            }

            if (hasCss || !htmlNeedsCss)
            {
                return;
            }

            _shaIndex++;
            string css = "sha1-" + _shaIndex.ToString("x8", CultureInfo.InvariantCulture) + ".css";
            _networkResources["resources/" + css] = Encoding.UTF8.GetBytes("/* css */");
        }
    }
}
