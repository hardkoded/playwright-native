/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IConsoleMessage"/> raised from browser console APIs.
    /// </summary>
    internal sealed partial class ConsoleMessage : IConsoleMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleMessage"/> class.
        /// </summary>
        /// <param name="type">Console type (<c>log</c>, <c>error</c>, ...).</param>
        /// <param name="text">Joined argument text.</param>
        /// <param name="location">Optional <c>URL:line:column</c>.</param>
        /// <param name="args">Optional JS handles. Null becomes empty.</param>
        /// <param name="page">The page that produced the message.</param>
        /// <param name="timestamp">Milliseconds since the Unix epoch. Zero uses the current time.</param>
        /// <param name="worker">The dedicated worker that produced the message, if any.</param>
        internal ConsoleMessage(
            string type,
            string text,
            string location,
            IReadOnlyList<IJSHandle> args,
            IPage page = null,
            double timestamp = 0,
            IWorker worker = null)
        {
            Type = type ?? "log";
            Text = text ?? string.Empty;
            Location = location ?? string.Empty;
            Args = args ?? Array.Empty<IJSHandle>();
            Page = page;
            Worker = worker;
            Timestamp = (float)(timestamp > 0
                ? timestamp
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <inheritdoc/>
        public IPage Page { get; internal set; }

        /// <inheritdoc/>
        public IWorker Worker { get; internal set; }

        /// <inheritdoc/>
        public float Timestamp { get; }

        /// <inheritdoc/>
        public IReadOnlyList<IJSHandle> Args { get; }

        /// <inheritdoc/>
        public string Location { get; }

        /// <inheritdoc/>
        public string Text { get; }

        /// <inheritdoc/>
        public string Type { get; }

        /// <summary>
        /// Official Node <c>util.inspect</c> for console messages returns
        /// <see cref="Text"/>.
        /// </summary>
        /// <returns>The joined console text.</returns>
        public override string ToString() => Text;
    }
}
