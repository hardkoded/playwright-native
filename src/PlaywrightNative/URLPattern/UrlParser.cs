/*
 * Copyright (c) 2026 Dario Kondratiuk
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
using System.Globalization;
using System.Text;
using static PlaywrightNative.UrlCodePoints;

namespace PlaywrightNative
{
    /// <summary>
    /// The WHATWG basic URL parser (https://url.spec.whatwg.org/#concept-basic-url-parser).
    /// It reports failures only, not validation errors, and always uses UTF-8.
    /// </summary>
    internal sealed class UrlParser
    {
        private readonly int[] _input;
        private readonly UrlRecord _base;
        private readonly UrlRecord _url;
        private readonly bool _hasOverride;
        private readonly UrlParserState? _stateOverride;
        private readonly StringBuilder _buffer = new();
        private UrlParserState _state;
        private int _pointer;
        private bool _atSignSeen;
        private bool _insideBrackets;
        private bool _passwordTokenSeen;

        private UrlParser(string input, UrlRecord baseUrl, UrlRecord url, UrlParserState? stateOverride)
        {
            if (url == null)
            {
                url = new UrlRecord();
                input = TrimC0ControlOrSpace(input);
            }

            input = input.Replace("\t", string.Empty, StringComparison.Ordinal)
                .Replace("\n", string.Empty, StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal);
            _input = ToCodePoints(input);
            _base = baseUrl;
            _url = url;
            _stateOverride = stateOverride;
            _hasOverride = stateOverride.HasValue;
            _state = stateOverride ?? UrlParserState.SchemeStart;
        }

        private enum Outcome
        {
            Continue,
            Return,
            Failure,
        }

        private bool IsSpecial => _url.IsSpecial;

        /// <summary>
        /// Parses <paramref name="input"/> against an optional base URL. Returns null on failure.
        /// </summary>
        public static UrlRecord Parse(string input, UrlRecord baseUrl = null)
        {
            var parser = new UrlParser(input, baseUrl, null, null);
            return parser.Run() ? parser._url : null;
        }

        /// <summary>
        /// Runs the parser on <paramref name="url"/>, starting in <paramref name="stateOverride"/>. Returns false on failure.
        /// </summary>
        public static bool ParseInto(string input, UrlRecord url, UrlParserState stateOverride)
            => new UrlParser(input, null, url, stateOverride).Run();

        public static bool IsWindowsDriveLetter(string value)
            => value.Length == 2 && IsAsciiAlpha(value[0]) && (value[1] == ':' || value[1] == '|');

        public static bool IsNormalizedWindowsDriveLetter(string value) => IsWindowsDriveLetter(value) && value[1] == ':';

        private static string TrimC0ControlOrSpace(string input)
        {
            int start = 0;
            int end = input.Length;
            while (start < end && input[start] <= 0x20)
            {
                start++;
            }

            while (end > start && input[end - 1] <= 0x20)
            {
                end--;
            }

            return input[start..end];
        }

        private static bool IsSingleDot(string segment) => segment == "." || segment.Equals("%2e", StringComparison.OrdinalIgnoreCase);

        private static bool IsDoubleDot(string segment) => ToAsciiLower(segment) is ".." or ".%2e" or "%2e." or "%2e%2e";

        private int CodePointAt(int index) => index >= 0 && index < _input.Length ? _input[index] : Eof;

        private bool RemainingStartsWith(char c) => CodePointAt(_pointer + 1) == c;

        private bool StartsWithWindowsDriveLetter(int index)
        {
            int length = _input.Length - index;
            if (length < 2 || !IsAsciiAlpha(_input[index]) || (_input[index + 1] != ':' && _input[index + 1] != '|'))
            {
                return false;
            }

            return length == 2 || _input[index + 2] is '/' or '\\' or '?' or '#';
        }

        private void CopyAuthorityFromBase()
        {
            _url.Username = _base.Username;
            _url.Password = _base.Password;
            _url.Host = _base.Host;
            _url.Port = _base.Port;
        }

        private bool Run()
        {
            for (; ; _pointer++)
            {
                Outcome outcome = RunState(CodePointAt(_pointer));
                if (outcome != Outcome.Continue)
                {
                    return outcome == Outcome.Return;
                }

                if (_pointer >= _input.Length)
                {
                    return true;
                }
            }
        }

        private Outcome RunState(int c) => _state switch
        {
            UrlParserState.SchemeStart => SchemeStartState(c),
            UrlParserState.Scheme => SchemeState(c),
            UrlParserState.NoScheme => NoSchemeState(c),
            UrlParserState.SpecialRelativeOrAuthority => SpecialRelativeOrAuthorityState(c),
            UrlParserState.PathOrAuthority => PathOrAuthorityState(c),
            UrlParserState.Relative => RelativeState(c),
            UrlParserState.RelativeSlash => RelativeSlashState(c),
            UrlParserState.SpecialAuthoritySlashes => SpecialAuthoritySlashesState(c),
            UrlParserState.SpecialAuthorityIgnoreSlashes => SpecialAuthorityIgnoreSlashesState(c),
            UrlParserState.Authority => AuthorityState(c),
            UrlParserState.Host or UrlParserState.Hostname => HostState(c),
            UrlParserState.Port => PortState(c),
            UrlParserState.File => FileState(c),
            UrlParserState.FileSlash => FileSlashState(c),
            UrlParserState.FileHost => FileHostState(c),
            UrlParserState.PathStart => PathStartState(c),
            UrlParserState.Path => PathState(c),
            UrlParserState.OpaquePath => OpaquePathState(c),
            UrlParserState.Query => QueryState(c),
            _ => FragmentState(c),
        };

        private Outcome SchemeStartState(int c)
        {
            if (IsAsciiAlpha(c))
            {
                _buffer.Append((char)ToAsciiLower(c));
                _state = UrlParserState.Scheme;
            }
            else if (!_hasOverride)
            {
                _state = UrlParserState.NoScheme;
                _pointer--;
            }
            else
            {
                return Outcome.Failure;
            }

            return Outcome.Continue;
        }

        private Outcome SchemeState(int c)
        {
            if (IsAsciiAlphanumeric(c) || c is '+' or '-' or '.')
            {
                _buffer.Append((char)ToAsciiLower(c));
                return Outcome.Continue;
            }

            if (c != ':')
            {
                if (_hasOverride)
                {
                    return Outcome.Failure;
                }

                _buffer.Clear();
                _state = UrlParserState.NoScheme;
                _pointer = -1;
                return Outcome.Continue;
            }

            string scheme = _buffer.ToString();
            if (_hasOverride)
            {
                if (_url.IsSpecial != UrlRecord.IsSpecialScheme(scheme)
                    || ((_url.IncludesCredentials || _url.Port.HasValue) && scheme == "file")
                    || (_url.Scheme == "file" && _url.Host?.Length == 0))
                {
                    return Outcome.Return;
                }
            }

            _url.Scheme = scheme;
            if (_hasOverride)
            {
                if (_url.Port == UrlRecord.DefaultPort(scheme))
                {
                    _url.Port = null;
                }

                return Outcome.Return;
            }

            _buffer.Clear();
            if (scheme == "file")
            {
                _state = UrlParserState.File;
            }
            else if (IsSpecial && _base != null && _base.Scheme == scheme)
            {
                _state = UrlParserState.SpecialRelativeOrAuthority;
            }
            else if (IsSpecial)
            {
                _state = UrlParserState.SpecialAuthoritySlashes;
            }
            else if (RemainingStartsWith('/'))
            {
                _state = UrlParserState.PathOrAuthority;
                _pointer++;
            }
            else
            {
                _url.OpaquePath = string.Empty;
                _state = UrlParserState.OpaquePath;
            }

            return Outcome.Continue;
        }

        private Outcome NoSchemeState(int c)
        {
            if (_base == null || (_base.HasOpaquePath && c != '#'))
            {
                return Outcome.Failure;
            }

            if (_base.HasOpaquePath)
            {
                _url.Scheme = _base.Scheme;
                _url.OpaquePath = _base.OpaquePath;
                _url.Query = _base.Query;
                _url.Fragment = string.Empty;
                _state = UrlParserState.Fragment;
            }
            else
            {
                _state = _base.Scheme != "file" ? UrlParserState.Relative : UrlParserState.File;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome SpecialRelativeOrAuthorityState(int c)
        {
            if (c == '/' && RemainingStartsWith('/'))
            {
                _state = UrlParserState.SpecialAuthorityIgnoreSlashes;
                _pointer++;
            }
            else
            {
                _state = UrlParserState.Relative;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome PathOrAuthorityState(int c)
        {
            if (c == '/')
            {
                _state = UrlParserState.Authority;
            }
            else
            {
                _state = UrlParserState.Path;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome RelativeState(int c)
        {
            _url.Scheme = _base.Scheme;
            if (c == '/' || (IsSpecial && c == '\\'))
            {
                _state = UrlParserState.RelativeSlash;
                return Outcome.Continue;
            }

            CopyAuthorityFromBase();
            _url.Path = [.. _base.Path];
            _url.Query = _base.Query;
            if (c == '?')
            {
                _url.Query = string.Empty;
                _state = UrlParserState.Query;
            }
            else if (c == '#')
            {
                _url.Fragment = string.Empty;
                _state = UrlParserState.Fragment;
            }
            else if (c != Eof)
            {
                _url.Query = null;
                _url.ShortenPath();
                _state = UrlParserState.Path;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome RelativeSlashState(int c)
        {
            if (IsSpecial && (c == '/' || c == '\\'))
            {
                _state = UrlParserState.SpecialAuthorityIgnoreSlashes;
            }
            else if (c == '/')
            {
                _state = UrlParserState.Authority;
            }
            else
            {
                CopyAuthorityFromBase();
                _state = UrlParserState.Path;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome SpecialAuthoritySlashesState(int c)
        {
            _state = UrlParserState.SpecialAuthorityIgnoreSlashes;
            if (c == '/' && RemainingStartsWith('/'))
            {
                _pointer++;
            }
            else
            {
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome SpecialAuthorityIgnoreSlashesState(int c)
        {
            if (c != '/' && c != '\\')
            {
                _state = UrlParserState.Authority;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome AuthorityState(int c)
        {
            if (c == '@')
            {
                if (_atSignSeen)
                {
                    _buffer.Insert(0, "%40");
                }

                _atSignSeen = true;
                var username = new StringBuilder(_url.Username);
                var password = new StringBuilder(_url.Password);
                foreach (int codePoint in ToCodePoints(_buffer.ToString()))
                {
                    if (codePoint == ':' && !_passwordTokenSeen)
                    {
                        _passwordTokenSeen = true;
                        continue;
                    }

                    AppendPercentEncoded(_passwordTokenSeen ? password : username, codePoint, PercentEncodeSet.Userinfo);
                }

                _url.Username = username.ToString();
                _url.Password = password.ToString();
                _buffer.Clear();
            }
            else if (c is Eof or '/' or '?' or '#' || (IsSpecial && c == '\\'))
            {
                if (_atSignSeen && _buffer.Length == 0)
                {
                    return Outcome.Failure;
                }

                _pointer -= CodePointLength(_buffer.ToString()) + 1;
                _buffer.Clear();
                _state = UrlParserState.Host;
            }
            else
            {
                AppendCodePoint(_buffer, c);
            }

            return Outcome.Continue;
        }

        private Outcome HostState(int c)
        {
            if (_hasOverride && _url.Scheme == "file")
            {
                _pointer--;
                _state = UrlParserState.FileHost;
            }
            else if (c == ':' && !_insideBrackets)
            {
                if (_buffer.Length == 0 || _stateOverride == UrlParserState.Hostname)
                {
                    return Outcome.Failure;
                }

                string host = UrlHostParser.Parse(_buffer.ToString(), !IsSpecial);
                if (host == null)
                {
                    return Outcome.Failure;
                }

                _url.Host = host;
                _buffer.Clear();
                _state = UrlParserState.Port;
            }
            else if (c is Eof or '/' or '?' or '#' || (IsSpecial && c == '\\'))
            {
                _pointer--;
                if (IsSpecial && _buffer.Length == 0)
                {
                    return Outcome.Failure;
                }

                if (_hasOverride && _buffer.Length == 0 && (_url.IncludesCredentials || _url.Port.HasValue))
                {
                    return Outcome.Failure;
                }

                string host = UrlHostParser.Parse(_buffer.ToString(), !IsSpecial);
                if (host == null)
                {
                    return Outcome.Failure;
                }

                _url.Host = host;
                _buffer.Clear();
                _state = UrlParserState.PathStart;
                if (_hasOverride)
                {
                    return Outcome.Return;
                }
            }
            else
            {
                if (c == '[')
                {
                    _insideBrackets = true;
                }
                else if (c == ']')
                {
                    _insideBrackets = false;
                }

                AppendCodePoint(_buffer, c);
            }

            return Outcome.Continue;
        }

        private Outcome PortState(int c)
        {
            if (IsAsciiDigit(c))
            {
                _buffer.Append((char)c);
                return Outcome.Continue;
            }

            if (c is Eof or '/' or '?' or '#' || (IsSpecial && c == '\\') || _hasOverride)
            {
                if (_buffer.Length != 0)
                {
                    string digits = _buffer.ToString().TrimStart('0');
                    if (digits.Length > 5 || (digits.Length > 0 && int.Parse(digits, CultureInfo.InvariantCulture) > 65535))
                    {
                        return Outcome.Failure;
                    }

                    int port = digits.Length == 0 ? 0 : int.Parse(digits, CultureInfo.InvariantCulture);
                    _url.Port = port == UrlRecord.DefaultPort(_url.Scheme) ? null : port;
                    _buffer.Clear();
                    if (_hasOverride)
                    {
                        return Outcome.Return;
                    }
                }

                if (_hasOverride)
                {
                    return Outcome.Failure;
                }

                _state = UrlParserState.PathStart;
                _pointer--;
                return Outcome.Continue;
            }

            return Outcome.Failure;
        }

        private Outcome FileState(int c)
        {
            _url.Scheme = "file";
            _url.Host = string.Empty;
            if (c == '/' || c == '\\')
            {
                _state = UrlParserState.FileSlash;
            }
            else if (_base != null && _base.Scheme == "file")
            {
                _url.Host = _base.Host;
                _url.Path = [.. _base.Path];
                _url.Query = _base.Query;
                if (c == '?')
                {
                    _url.Query = string.Empty;
                    _state = UrlParserState.Query;
                }
                else if (c == '#')
                {
                    _url.Fragment = string.Empty;
                    _state = UrlParserState.Fragment;
                }
                else if (c != Eof)
                {
                    _url.Query = null;
                    if (!StartsWithWindowsDriveLetter(_pointer))
                    {
                        _url.ShortenPath();
                    }
                    else
                    {
                        _url.Path.Clear();
                    }

                    _state = UrlParserState.Path;
                    _pointer--;
                }
            }
            else
            {
                _state = UrlParserState.Path;
                _pointer--;
            }

            return Outcome.Continue;
        }

        private Outcome FileSlashState(int c)
        {
            if (c == '/' || c == '\\')
            {
                _state = UrlParserState.FileHost;
                return Outcome.Continue;
            }

            if (_base != null && _base.Scheme == "file")
            {
                _url.Host = _base.Host;
                if (!StartsWithWindowsDriveLetter(_pointer) && _base.Path.Count > 0 && IsNormalizedWindowsDriveLetter(_base.Path[0]))
                {
                    _url.Path.Add(_base.Path[0]);
                }
            }

            _state = UrlParserState.Path;
            _pointer--;
            return Outcome.Continue;
        }

        private Outcome FileHostState(int c)
        {
            if (c is not (Eof or '/' or '\\' or '?' or '#'))
            {
                AppendCodePoint(_buffer, c);
                return Outcome.Continue;
            }

            _pointer--;
            if (!_hasOverride && IsWindowsDriveLetter(_buffer.ToString()))
            {
                _state = UrlParserState.Path;
            }
            else if (_buffer.Length == 0)
            {
                _url.Host = string.Empty;
                if (_hasOverride)
                {
                    return Outcome.Return;
                }

                _state = UrlParserState.PathStart;
            }
            else
            {
                string host = UrlHostParser.Parse(_buffer.ToString(), !IsSpecial);
                if (host == null)
                {
                    return Outcome.Failure;
                }

                _url.Host = host == "localhost" ? string.Empty : host;
                if (_hasOverride)
                {
                    return Outcome.Return;
                }

                _buffer.Clear();
                _state = UrlParserState.PathStart;
            }

            return Outcome.Continue;
        }

        private Outcome PathStartState(int c)
        {
            if (IsSpecial)
            {
                _state = UrlParserState.Path;
                if (c != '/' && c != '\\')
                {
                    _pointer--;
                }
            }
            else if (!_hasOverride && c == '?')
            {
                _url.Query = string.Empty;
                _state = UrlParserState.Query;
            }
            else if (!_hasOverride && c == '#')
            {
                _url.Fragment = string.Empty;
                _state = UrlParserState.Fragment;
            }
            else if (c != Eof)
            {
                _state = UrlParserState.Path;
                if (c != '/')
                {
                    _pointer--;
                }
            }
            else if (_hasOverride && _url.Host == null)
            {
                _url.Path.Add(string.Empty);
            }

            return Outcome.Continue;
        }

        private Outcome PathState(int c)
        {
            bool slash = c == '/' || (IsSpecial && c == '\\');
            if (c == Eof || slash || (!_hasOverride && c is '?' or '#'))
            {
                string segment = _buffer.ToString();
                if (IsDoubleDot(segment))
                {
                    _url.ShortenPath();
                    if (!slash)
                    {
                        _url.Path.Add(string.Empty);
                    }
                }
                else if (IsSingleDot(segment) && !slash)
                {
                    _url.Path.Add(string.Empty);
                }
                else if (!IsSingleDot(segment))
                {
                    if (_url.Scheme == "file" && _url.Path.Count == 0 && IsWindowsDriveLetter(segment))
                    {
                        segment = segment[0] + ":";
                    }

                    _url.Path.Add(segment);
                }

                _buffer.Clear();
                if (c == '?')
                {
                    _url.Query = string.Empty;
                    _state = UrlParserState.Query;
                }
                else if (c == '#')
                {
                    _url.Fragment = string.Empty;
                    _state = UrlParserState.Fragment;
                }
            }
            else
            {
                AppendPercentEncoded(_buffer, c, PercentEncodeSet.Path);
            }

            return Outcome.Continue;
        }

        private Outcome OpaquePathState(int c)
        {
            if (c == '?')
            {
                _url.Query = string.Empty;
                _state = UrlParserState.Query;
            }
            else if (c == '#')
            {
                _url.Fragment = string.Empty;
                _state = UrlParserState.Fragment;
            }
            else if (c == ' ')
            {
                _url.OpaquePath += RemainingStartsWith('?') || RemainingStartsWith('#') ? "%20" : " ";
            }
            else if (c != Eof)
            {
                var encoded = new StringBuilder();
                AppendPercentEncoded(encoded, c, PercentEncodeSet.C0Control);
                _url.OpaquePath += encoded.ToString();
            }

            return Outcome.Continue;
        }

        private Outcome QueryState(int c)
        {
            if ((!_hasOverride && c == '#') || c == Eof)
            {
                _url.Query += PercentEncode(_buffer.ToString(), IsSpecial ? PercentEncodeSet.SpecialQuery : PercentEncodeSet.Query);
                _buffer.Clear();
                if (c == '#')
                {
                    _url.Fragment = string.Empty;
                    _state = UrlParserState.Fragment;
                }
            }
            else
            {
                AppendCodePoint(_buffer, c);
            }

            return Outcome.Continue;
        }

        private Outcome FragmentState(int c)
        {
            if (c != Eof)
            {
                var encoded = new StringBuilder();
                AppendPercentEncoded(encoded, c, PercentEncodeSet.Fragment);
                _url.Fragment += encoded.ToString();
            }

            return Outcome.Continue;
        }
    }
}
