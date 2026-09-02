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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// WebKit <c>Response.formData()</c> workaround for the closing-delimiter CRLF
    /// check (https://bugs.webkit.org/show_bug.cgi?id=312136, Playwright 40244).
    /// </summary>
    internal static class WebKitFormDataScript
    {
        /// <summary>
        /// Bootstrap source installed on every WebKit document. Native
        /// <c>formData()</c> is used first; only a multipart TypeError retries
        /// with a trailing CRLF appended to the body.
        /// </summary>
        internal const string Source =
            @"(function() {
  if (typeof Response === 'undefined' || !Response.prototype.formData || Response.prototype.formData.__pwMultipartCrlf) {
    return;
  }
  const original = Response.prototype.formData;
  async function formData() {
    const contentType = this.headers && this.headers.get('Content-Type');
    const multipart = contentType && contentType.toLowerCase().indexOf('multipart/form-data') >= 0;
    let spare = null;
    if (multipart) {
      try { spare = this.clone(); } catch (e) { spare = null; }
    }
    try {
      return await original.call(this);
    } catch (error) {
      if (!spare || !multipart) {
        throw error;
      }
      const bytes = new Uint8Array(await spare.arrayBuffer());
      if (bytes.length >= 2 && bytes[bytes.length - 2] === 13 && bytes[bytes.length - 1] === 10) {
        throw error;
      }
      const padded = new Uint8Array(bytes.length + 2);
      padded.set(bytes, 0);
      padded[bytes.length] = 13;
      padded[bytes.length + 1] = 10;
      return original.call(new Response(padded, { headers: { 'Content-Type': contentType } }));
    }
  }
  formData.__pwMultipartCrlf = true;
  Response.prototype.formData = formData;
})();
";
    }
}
