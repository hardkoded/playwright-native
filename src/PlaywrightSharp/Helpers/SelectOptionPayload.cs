/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using PlaywrightSharp.Input;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// JSON for <see cref="ElementStateScript.SelectOptionFromJsonFunction"/>.
    /// </summary>
    internal static class SelectOptionPayload
    {
        /// <summary>
        /// Serializes option <c>value</c> strings. A bare string matches value or label
        /// (upstream <c>selectOption(selector, 'Blue')</c> fallback) unless
        /// <paramref name="matchLabel"/> is <see langword="false"/>.
        /// </summary>
        /// <param name="values">Values to select. Null is treated as empty (unselect).</param>
        /// <param name="matchLabel">When <see langword="true"/>, match value or label.</param>
        /// <returns>A JSON array string.</returns>
        internal static string FromValues(IEnumerable<string> values, bool matchLabel = true)
        {
            List<object> list = new List<object>();
            if (values != null)
            {
                int index = 0;
                foreach (string value in values)
                {
                    if (value == null)
                    {
                        throw new PlaywrightSharpException(
                            "options[" + index.ToString(CultureInfo.InvariantCulture) + "]: expected object, got null");
                    }

                    if (matchLabel)
                    {
                        list.Add(new { valueOrLabel = value });
                    }
                    else
                    {
                        list.Add(new { value });
                    }

                    index++;
                }
            }

            return JsonSerializer.Serialize(list);
        }

        /// <summary>
        /// Serializes <see cref="SelectOptionValue"/> descriptors.
        /// </summary>
        /// <param name="values">Descriptors. Null is treated as empty (unselect).</param>
        /// <returns>A JSON array string.</returns>
        internal static string FromOptions(IEnumerable<SelectOptionValue> values)
        {
            List<object> list = new List<object>();
            if (values != null)
            {
                int index = 0;
                foreach (SelectOptionValue value in values)
                {
                    if (value == null)
                    {
                        throw new PlaywrightSharpException(
                            "options[" + index.ToString(CultureInfo.InvariantCulture) + "]: expected object, got null");
                    }

                    list.Add(new
                    {
                        value = value.Value,
                        label = value.Label,
                        index = value.Index,
                    });
                    index++;
                }
            }

            return JsonSerializer.Serialize(list);
        }

        /// <summary>
        /// Serializes internal <see cref="SelectOption"/> descriptors.
        /// </summary>
        /// <param name="values">Descriptors. Null is treated as empty.</param>
        /// <returns>A JSON array string.</returns>
        internal static string FromInputOptions(IEnumerable<SelectOption> values)
        {
            List<object> list = new List<object>();
            if (values != null)
            {
                int index = 0;
                foreach (SelectOption value in values)
                {
                    if (value == null)
                    {
                        throw new PlaywrightSharpException(
                            "options[" + index.ToString(CultureInfo.InvariantCulture) + "]: expected object, got null");
                    }

                    if (value.ValueOrLabel != null)
                    {
                        list.Add(new { valueOrLabel = value.ValueOrLabel });
                    }
                    else
                    {
                        list.Add(new
                        {
                            value = value.Value,
                            label = value.Label,
                            index = value.Index,
                        });
                    }

                    index++;
                }
            }

            return JsonSerializer.Serialize(list);
        }
    }
}
