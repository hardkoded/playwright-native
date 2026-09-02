/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Evaluates <c>localStorage</c> or <c>sessionStorage</c> on the page's
    /// current origin.
    /// </summary>
    internal sealed partial class WebStorage : IWebStorage
    {
        private readonly IPage _page;
        private readonly string _storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebStorage"/> class.
        /// </summary>
        /// <param name="page">The page whose origin is queried.</param>
        /// <param name="storage">
        /// <c>localStorage</c> or <c>sessionStorage</c>.
        /// </param>
        internal WebStorage(IPage page, string storage)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WebStorageItem>> ItemsAsync()
        {
            string json = await _page.EvaluateAsync<string>(
                    "(() => { const s = " + _storage + "; const items = [];" +
                    "for (let i = 0; i < s.length; i++) {" +
                    "const name = s.key(i);" +
                    "items.push({ name: name, value: s.getItem(name) }); }" +
                    "return JSON.stringify(items); })()")
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<WebStorageItem>();
            }

            using JsonDocument document = JsonDocument.Parse(json);
            List<WebStorageItem> items = new List<WebStorageItem>();
            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                items.Add(new WebStorageItem
                {
                    Name = entry.GetProperty("name").GetString(),
                    Value = entry.GetProperty("value").GetString(),
                });
            }

            return items;
        }

        /// <inheritdoc/>
        public Task<string> GetItemAsync(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _page.EvaluateAsync<string>(
                "(() => " + _storage + ".getItem(" + JsonSerializer.Serialize(name) + "))()");
        }

        /// <inheritdoc/>
        public Task SetItemAsync(string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return _page.EvaluateAsync<object>(
                "(() => { " + _storage + ".setItem(" +
                JsonSerializer.Serialize(name) + ", " +
                JsonSerializer.Serialize(value) + "); })()");
        }

        /// <inheritdoc/>
        public Task RemoveItemAsync(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _page.EvaluateAsync<object>(
                "(() => { " + _storage + ".removeItem(" +
                JsonSerializer.Serialize(name) + "); })()");
        }

        /// <inheritdoc/>
        public Task ClearAsync()
            => _page.EvaluateAsync<object>("(() => { " + _storage + ".clear(); })()");
    }
}
