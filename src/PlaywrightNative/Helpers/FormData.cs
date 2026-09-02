/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// In-memory <see cref="IFormData"/>.
    /// </summary>
    internal sealed partial class FormData : IFormData
    {
        private readonly List<Field> _fields = new List<Field>();

        /// <inheritdoc/>
        public IFormData Set(string name, string value)
        {
            Upsert(name, value ?? string.Empty, null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, bool value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, int value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, long value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, double value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, decimal value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, float value)
        {
            Upsert(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Set(string name, FilePayload file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            Upsert(name, null, file);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, string value)
        {
            Add(name, value ?? string.Empty, null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, bool value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, int value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, long value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, double value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, decimal value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, float value)
        {
            Add(name, FormatValue(value), null);
            return this;
        }

        /// <inheritdoc/>
        public IFormData Append(string name, FilePayload file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            Add(name, null, file);
            return this;
        }

        /// <summary>
        /// Whether any field is a <see cref="FilePayload"/>.
        /// </summary>
        /// <returns><see langword="true"/> when a file field is present.</returns>
        internal bool ContainsFiles()
        {
            foreach (Field field in _fields)
            {
                if (field.File != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fields in insertion order. <see cref="Set(string, string)"/> and
        /// <see cref="Set(string, FilePayload)"/> replace every field with
        /// the same name. <see cref="Append(string, string)"/> and
        /// <see cref="Append(string, FilePayload)"/> add another field.
        /// </summary>
        /// <returns>The form fields.</returns>
        internal IReadOnlyList<KeyValuePair<string, string>> GetEntries()
        {
            List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();
            foreach (Field field in _fields)
            {
                if (field.File == null)
                {
                    entries.Add(new KeyValuePair<string, string>(field.Name, field.Text ?? string.Empty));
                }
            }

            return entries;
        }

        /// <summary>
        /// All fields, including files, in insertion order.
        /// </summary>
        /// <returns>The fields.</returns>
        internal IReadOnlyList<(string Name, string Text, FilePayload File)> GetFields()
        {
            List<(string Name, string Text, FilePayload File)> fields =
                new List<(string Name, string Text, FilePayload File)>();
            foreach (Field field in _fields)
            {
                fields.Add((field.Name, field.Text, field.File));
            }

            return fields;
        }

        private static string FormatValue(bool value) => value ? "true" : "false";

        private static string FormatValue(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatValue(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatValue(double value) => value.ToString("G", CultureInfo.InvariantCulture);

        private static string FormatValue(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatValue(float value) => value.ToString("G", CultureInfo.InvariantCulture);

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Form field name must be non-empty.", nameof(name));
            }
        }

        private void Upsert(string name, string text, FilePayload file)
        {
            ValidateName(name);
            _fields.RemoveAll(field => string.Equals(field.Name, name, StringComparison.Ordinal));
            _fields.Add(new Field { Name = name, Text = text, File = file });
        }

        private void Add(string name, string text, FilePayload file)
        {
            ValidateName(name);
            _fields.Add(new Field { Name = name, Text = text, File = file });
        }

        private sealed class Field
        {
            internal string Name { get; set; }

            internal string Text { get; set; }

            internal FilePayload File { get; set; }
        }
    }
}
