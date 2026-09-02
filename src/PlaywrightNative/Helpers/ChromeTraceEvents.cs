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
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Synthetic Chrome-trace events for official <c>tracing.group</c>.
    /// </summary>
    internal static class ChromeTraceEvents
    {
        /// <summary>
        /// Builds a duration-begin event whose <c>name</c> is the group title.
        /// </summary>
        /// <param name="name">Group name shown in the trace.</param>
        /// <returns>A cloned <see cref="JsonElement"/> ready to store.</returns>
        internal static JsonElement GroupBegin(string name) => GroupPhase(name, "B");

        /// <summary>
        /// Builds a duration-end event that closes <see cref="GroupBegin"/>.
        /// </summary>
        /// <param name="name">Group name shown in the trace.</param>
        /// <returns>A cloned <see cref="JsonElement"/> ready to store.</returns>
        internal static JsonElement GroupEnd(string name) => GroupPhase(name, "E");

        /// <summary>
        /// Writes a Chrome trace JSON object with a <c>traceEvents</c> array.
        /// </summary>
        /// <param name="path">Destination file path.</param>
        /// <param name="events">Events to serialize.</param>
        /// <returns>A task that completes when the file has been written.</returns>
        internal static async Task WriteAsync(string path, IReadOnlyList<JsonElement> events)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(path);
            using Utf8JsonWriter writer = new(stream);
            writer.WriteStartObject();
            writer.WritePropertyName("traceEvents");
            writer.WriteStartArray();
            foreach (JsonElement item in events)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync().ConfigureAwait(false);
        }

        private static JsonElement GroupPhase(string name, string phase)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
            string json = "{\"name\":" + JsonSerializer.Serialize(name)
                + ",\"cat\":\"playwright\",\"ph\":" + JsonSerializer.Serialize(phase)
                + ",\"ts\":"
                + timestamp.ToString(CultureInfo.InvariantCulture)
                + ",\"pid\":1,\"tid\":1}";
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
