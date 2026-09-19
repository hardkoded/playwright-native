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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Serializes storage-state cookies including Chromium
    /// <c>_crHasCrossSiteAncestor</c> (not a public <see cref="Cookie"/> property).
    /// </summary>
    internal sealed class StorageStateCookieJsonConverter : JsonConverter<Cookie>
    {
        /// <inheritdoc/>
        public override Cookie Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected cookie object.");
            }

            Cookie cookie = new Cookie();
            bool? hasCrossSiteAncestor = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    CookieExtras.SetHasCrossSiteAncestor(cookie, hasCrossSiteAncestor);
                    return cookie;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected cookie property name.");
                }

                string name = reader.GetString();
                reader.Read();
                if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Name = reader.GetString();
                }
                else if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Value = reader.GetString();
                }
                else if (string.Equals(name, "url", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Url = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                }
                else if (string.Equals(name, "domain", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Domain = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                }
                else if (string.Equals(name, "path", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Path = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                }
                else if (string.Equals(name, "expires", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        cookie.Expires = reader.GetSingle();
                    }
                }
                else if (string.Equals(name, "httpOnly", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                    {
                        cookie.HttpOnly = reader.GetBoolean();
                    }
                }
                else if (string.Equals(name, "secure", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                    {
                        cookie.Secure = reader.GetBoolean();
                    }
                }
                else if (string.Equals(name, "sameSite", StringComparison.OrdinalIgnoreCase))
                {
                    string sameSite = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    if (string.Equals(sameSite, "Strict", StringComparison.OrdinalIgnoreCase))
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.Strict;
                    }
                    else if (string.Equals(sameSite, "None", StringComparison.OrdinalIgnoreCase))
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.None;
                    }
                    else if (string.Equals(sameSite, "Lax", StringComparison.OrdinalIgnoreCase))
                    {
                        cookie.SameSite = Microsoft.Playwright.SameSiteAttribute.Lax;
                    }
                }
                else if (string.Equals(name, "partitionKey", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.PartitionKey = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                }
                else if (string.Equals(name, "_crHasCrossSiteAncestor", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                    {
                        hasCrossSiteAncestor = reader.GetBoolean();
                    }
                }
                else
                {
                    reader.Skip();
                }
            }

            throw new JsonException("Unexpected end of cookie object.");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, Cookie value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteString("name", value.Name ?? string.Empty);
            writer.WriteString("value", value.Value ?? string.Empty);
            if (!string.IsNullOrEmpty(value.Url))
            {
                writer.WriteString("url", value.Url);
            }

            if (!string.IsNullOrEmpty(value.Domain))
            {
                writer.WriteString("domain", value.Domain);
            }

            if (!string.IsNullOrEmpty(value.Path))
            {
                writer.WriteString("path", value.Path);
            }

            if (value.Expires.HasValue)
            {
                writer.WriteNumber("expires", value.Expires.Value);
            }

            if (value.HttpOnly.HasValue)
            {
                writer.WriteBoolean("httpOnly", value.HttpOnly.Value);
            }

            if (value.Secure.HasValue)
            {
                writer.WriteBoolean("secure", value.Secure.Value);
            }

            if (value.SameSite.HasValue)
            {
                writer.WriteString("sameSite", value.SameSite.Value.ToString());
            }

            if (!string.IsNullOrEmpty(value.PartitionKey))
            {
                writer.WriteString("partitionKey", value.PartitionKey);
            }

            bool? hasCrossSiteAncestor = CookieExtras.GetHasCrossSiteAncestor(value);
            if (hasCrossSiteAncestor.HasValue)
            {
                writer.WriteBoolean("_crHasCrossSiteAncestor", hasCrossSiteAncestor.Value);
            }

            writer.WriteEndObject();
        }
    }
}
