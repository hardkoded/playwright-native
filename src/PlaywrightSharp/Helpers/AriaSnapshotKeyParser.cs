// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.Text;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official Playwright aria snapshot key parser
    /// (<c>role "name" [attr=value]</c>).
    /// </summary>
    internal static class AriaSnapshotKeyParser
    {
        internal static AriaSnapshotTemplate Parse(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return new Reader(input).Parse();
        }

        private sealed class Reader
        {
            private readonly string _input;
            private int _pos;

            internal Reader(string input)
            {
                _input = input;
                _pos = 0;
            }

            internal AriaSnapshotTemplate Parse()
            {
                SkipWhite();
                string role = ReadIdentifier("role");
                SkipWhite();
                AriaSnapshotTemplate node = new AriaSnapshotTemplate { Role = role };
                if (Peek() == '"' || Peek() == '/')
                {
                    string name = ReadStringOrRegex(out bool regex);
                    if (regex)
                    {
                        AriaSnapshotTemplateParser.ApplyTextValue(node, "/" + name + "/", isName: true);
                    }
                    else
                    {
                        node.Name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(name);
                    }
                }

                ReadAttributes(node);
                SkipWhite();
                if (!Eof())
                {
                    Throw("Unexpected input");
                }

                return node;
            }

            private void ReadAttributes(AriaSnapshotTemplate node)
            {
                while (true)
                {
                    SkipWhite();
                    if (Peek() != '[')
                    {
                        break;
                    }

                    Next();
                    SkipWhite();
                    int errorPos = _pos;
                    string flagName = ReadIdentifier("attribute");
                    SkipWhite();
                    string flagValue = "true";
                    if (Peek() == '=')
                    {
                        Next();
                        SkipWhite();
                        errorPos = _pos;
                        StringBuilder value = new StringBuilder();
                        while (!Eof() && Peek() != ']' && !char.IsWhiteSpace(Peek()))
                        {
                            value.Append(Next());
                        }

                        flagValue = value.ToString();
                    }

                    SkipWhite();
                    if (Peek() != ']')
                    {
                        Throw("Expected ]");
                    }

                    Next();
                    ApplyAttribute(node, flagName, flagValue, errorPos);
                }
            }

            private void ApplyAttribute(AriaSnapshotTemplate node, string key, string value, int errorPos)
            {
                if (string.Equals(key, "checked", StringComparison.Ordinal))
                {
                    Assert(
                        value == "true" || value == "false" || value == "mixed",
                        "Value of \"checked\" attribute must be a boolean or \"mixed\"",
                        errorPos);
                    node.Checked = value;
                    return;
                }

                if (string.Equals(key, "disabled", StringComparison.Ordinal))
                {
                    Assert(value == "true" || value == "false", "Value of \"disabled\" attribute must be a boolean", errorPos);
                    node.Disabled = value == "true";
                    return;
                }

                if (string.Equals(key, "expanded", StringComparison.Ordinal))
                {
                    Assert(value == "true" || value == "false", "Value of \"expanded\" attribute must be a boolean", errorPos);
                    node.Expanded = value == "true";
                    return;
                }

                if (string.Equals(key, "level", StringComparison.Ordinal))
                {
                    Assert(
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level),
                        "Value of \"level\" attribute must be a number",
                        errorPos);
                    node.Level = level;
                    return;
                }

                if (string.Equals(key, "pressed", StringComparison.Ordinal))
                {
                    Assert(
                        value == "true" || value == "false" || value == "mixed",
                        "Value of \"pressed\" attribute must be a boolean or \"mixed\"",
                        errorPos);
                    node.Pressed = value;
                    return;
                }

                if (string.Equals(key, "selected", StringComparison.Ordinal))
                {
                    Assert(value == "true" || value == "false", "Value of \"selected\" attribute must be a boolean", errorPos);
                    node.Selected = value == "true";
                    return;
                }

                if (string.Equals(key, "invalid", StringComparison.Ordinal))
                {
                    node.Invalid = value;
                    return;
                }

                Assert(false, "Unsupported attribute [" + key + "]", errorPos);
            }

            private string ReadStringOrRegex(out bool regex)
            {
                if (Peek() == '"')
                {
                    Next();
                    regex = false;
                    return ReadString();
                }

                Next();
                regex = true;
                return ReadRegex();
            }

            private string ReadString()
            {
                StringBuilder result = new StringBuilder();
                bool escaped = false;
                while (!Eof())
                {
                    char ch = Next();
                    if (escaped)
                    {
                        result.Append(ch);
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        return result.ToString();
                    }
                    else
                    {
                        result.Append(ch);
                    }
                }

                Throw("Unterminated string");
                return string.Empty;
            }

            private string ReadRegex()
            {
                StringBuilder result = new StringBuilder();
                bool escaped = false;
                bool insideClass = false;
                while (!Eof())
                {
                    char ch = Next();
                    if (escaped)
                    {
                        result.Append(ch);
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                        result.Append(ch);
                    }
                    else if (ch == '/' && !insideClass)
                    {
                        return result.ToString();
                    }
                    else if (ch == '[')
                    {
                        insideClass = true;
                        result.Append(ch);
                    }
                    else if (ch == ']' && insideClass)
                    {
                        result.Append(ch);
                        insideClass = false;
                    }
                    else
                    {
                        result.Append(ch);
                    }
                }

                Throw("Unterminated regex");
                return string.Empty;
            }

            private string ReadIdentifier(string type)
            {
                if (Eof())
                {
                    Throw("Unexpected end of input when expecting " + type);
                }

                int start = _pos;
                while (!Eof() && char.IsLetter(Peek()))
                {
                    _pos++;
                }

                return _input.Substring(start, _pos - start);
            }

            private void Assert(bool ok, string message, int pos)
            {
                if (!ok)
                {
                    Throw(message, pos);
                }
            }

            private void Throw(string message)
            {
                Throw(message, _pos);
            }

            private void Throw(string message, int pos)
            {
                string caret = new string(' ', pos) + "^";
                throw new AriaSnapshotParseException(message + ":\n\n" + _input + "\n" + caret + "\n");
            }

            private void SkipWhite()
            {
                while (!Eof() && char.IsWhiteSpace(Peek()))
                {
                    _pos++;
                }
            }

            private char Peek() => Eof() ? '\0' : _input[_pos];

            private char Next() => Eof() ? '\0' : _input[_pos++];

            private bool Eof() => _pos >= _input.Length;
        }
    }
}
