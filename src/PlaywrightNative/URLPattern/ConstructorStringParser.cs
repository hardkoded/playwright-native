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
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// Splits a URL pattern string such as "https://*.example.com/:id" into components
    /// (https://urlpattern.spec.whatwg.org/#constructor-string-parsing).
    /// </summary>
    internal sealed class ConstructorStringParser
    {
        private readonly string _input;
        private readonly List<PatternToken> _tokens;
        private readonly URLPatternFields _result = new();
        private int _componentStart;
        private int _tokenIndex;
        private int _tokenIncrement = 1;
        private int _groupDepth;
        private int _hostnameIPv6BracketDepth;
        private bool _protocolMatchesSpecialScheme;
        private State _state = State.Init;

        private ConstructorStringParser(string input)
        {
            _input = input;
            _tokens = PatternTokenizer.Tokenize(input, lenient: true);
        }

        private enum State
        {
            Init,
            Protocol,
            Authority,
            Username,
            Password,
            Hostname,
            Port,
            Pathname,
            Search,
            Hash,
            Done,
        }

        public static URLPatternFields Parse(string input)
        {
            var parser = new ConstructorStringParser(input);
            parser.Run();
            return parser._result;
        }

        private void Run()
        {
            while (_tokenIndex < _tokens.Count)
            {
                _tokenIncrement = 1;
                if (_tokens[_tokenIndex].Type == PatternTokenType.End)
                {
                    if (_state == State.Init)
                    {
                        // No protocol terminator, so this is a relative pattern.
                        Rewind();
                        if (IsHashPrefix())
                        {
                            ChangeState(State.Hash, 1);
                        }
                        else if (IsSearchPrefix())
                        {
                            ChangeState(State.Search, 1);
                        }
                        else
                        {
                            ChangeState(State.Pathname, 0);
                        }

                        _tokenIndex += _tokenIncrement;
                        continue;
                    }

                    if (_state == State.Authority)
                    {
                        // No "@", so there is no username or password.
                        RewindAndSetState(State.Hostname);
                        _tokenIndex += _tokenIncrement;
                        continue;
                    }

                    ChangeState(State.Done, 0);
                    break;
                }

                if (_tokens[_tokenIndex].Type == PatternTokenType.Open)
                {
                    _groupDepth++;
                    _tokenIndex += _tokenIncrement;
                    continue;
                }

                if (_groupDepth > 0)
                {
                    if (_tokens[_tokenIndex].Type == PatternTokenType.Close)
                    {
                        _groupDepth--;
                    }
                    else
                    {
                        _tokenIndex += _tokenIncrement;
                        continue;
                    }
                }

                RunState();
                _tokenIndex += _tokenIncrement;
            }

            if (_result.Hostname != null && _result.Port == null)
            {
                _result.Port = string.Empty;
            }
        }

        private void RunState()
        {
            switch (_state)
            {
                case State.Init:
                    if (IsNonSpecialPatternChar(_tokenIndex, ":"))
                    {
                        RewindAndSetState(State.Protocol);
                    }

                    break;
                case State.Protocol:
                    if (IsNonSpecialPatternChar(_tokenIndex, ":"))
                    {
                        ComputeProtocolMatchesSpecialSchemeFlag();
                        State nextState = State.Pathname;
                        int skip = 1;
                        if (IsNonSpecialPatternChar(_tokenIndex + 1, "/") && IsNonSpecialPatternChar(_tokenIndex + 2, "/"))
                        {
                            nextState = State.Authority;
                            skip = 3;
                        }
                        else if (_protocolMatchesSpecialScheme)
                        {
                            nextState = State.Authority;
                        }

                        ChangeState(nextState, skip);
                    }

                    break;
                case State.Authority:
                    if (IsNonSpecialPatternChar(_tokenIndex, "@"))
                    {
                        RewindAndSetState(State.Username);
                    }
                    else if (IsNonSpecialPatternChar(_tokenIndex, "/") || IsSearchPrefix() || IsHashPrefix())
                    {
                        RewindAndSetState(State.Hostname);
                    }

                    break;
                case State.Username:
                    if (IsNonSpecialPatternChar(_tokenIndex, ":"))
                    {
                        ChangeState(State.Password, 1);
                    }
                    else if (IsNonSpecialPatternChar(_tokenIndex, "@"))
                    {
                        ChangeState(State.Hostname, 1);
                    }

                    break;
                case State.Password:
                    if (IsNonSpecialPatternChar(_tokenIndex, "@"))
                    {
                        ChangeState(State.Hostname, 1);
                    }

                    break;
                case State.Hostname:
                    if (IsNonSpecialPatternChar(_tokenIndex, "["))
                    {
                        _hostnameIPv6BracketDepth++;
                    }
                    else if (IsNonSpecialPatternChar(_tokenIndex, "]"))
                    {
                        _hostnameIPv6BracketDepth--;
                    }
                    else if (IsNonSpecialPatternChar(_tokenIndex, ":") && _hostnameIPv6BracketDepth == 0)
                    {
                        ChangeState(State.Port, 1);
                    }
                    else
                    {
                        ChangeStateAtPathnameSearchOrHash();
                    }

                    break;
                case State.Port:
                    ChangeStateAtPathnameSearchOrHash();
                    break;
                case State.Pathname:
                    if (IsSearchPrefix())
                    {
                        ChangeState(State.Search, 1);
                    }
                    else if (IsHashPrefix())
                    {
                        ChangeState(State.Hash, 1);
                    }

                    break;
                case State.Search:
                    if (IsHashPrefix())
                    {
                        ChangeState(State.Hash, 1);
                    }

                    break;
            }
        }

        private void ChangeStateAtPathnameSearchOrHash()
        {
            if (IsNonSpecialPatternChar(_tokenIndex, "/"))
            {
                ChangeState(State.Pathname, 0);
            }
            else if (IsSearchPrefix())
            {
                ChangeState(State.Search, 1);
            }
            else if (IsHashPrefix())
            {
                ChangeState(State.Hash, 1);
            }
        }

        private void ChangeState(State newState, int skip)
        {
            if (_state is not (State.Init or State.Authority or State.Done))
            {
                SetComponent(_state, MakeComponentString());
            }

            if (_state != State.Init && newState != State.Done)
            {
                bool beforeHostname = _state is State.Protocol or State.Authority or State.Username or State.Password;
                if (beforeHostname && newState is (State.Port or State.Pathname or State.Search or State.Hash) && _result.Hostname == null)
                {
                    _result.Hostname = string.Empty;
                }

                bool beforePathname = beforeHostname || _state is State.Hostname or State.Port;
                if (beforePathname && newState is (State.Search or State.Hash) && _result.Pathname == null)
                {
                    _result.Pathname = _protocolMatchesSpecialScheme ? "/" : string.Empty;
                }

                bool beforeSearch = beforePathname || _state == State.Pathname;
                if (beforeSearch && newState == State.Hash && _result.Search == null)
                {
                    _result.Search = string.Empty;
                }
            }

            _state = newState;
            _tokenIndex += skip;
            _componentStart = _tokenIndex;
            _tokenIncrement = 0;
        }

        private void SetComponent(State state, string value)
        {
            switch (state)
            {
                case State.Protocol:
                    _result.Protocol = value;
                    break;
                case State.Username:
                    _result.Username = value;
                    break;
                case State.Password:
                    _result.Password = value;
                    break;
                case State.Hostname:
                    _result.Hostname = value;
                    break;
                case State.Port:
                    _result.Port = value;
                    break;
                case State.Pathname:
                    _result.Pathname = value;
                    break;
                case State.Search:
                    _result.Search = value;
                    break;
                case State.Hash:
                    _result.Hash = value;
                    break;
            }
        }

        private void Rewind()
        {
            _tokenIndex = _componentStart;
            _tokenIncrement = 0;
        }

        private void RewindAndSetState(State state)
        {
            Rewind();
            _state = state;
        }

        private PatternToken GetSafeToken(int index) => index < _tokens.Count ? _tokens[index] : _tokens[^1];

        private bool IsNonSpecialPatternChar(int index, string value)
        {
            PatternToken token = GetSafeToken(index);
            return token.Value == value && token.Type is (PatternTokenType.Char or PatternTokenType.EscapedChar or PatternTokenType.InvalidChar);
        }

        private bool IsSearchPrefix()
        {
            if (IsNonSpecialPatternChar(_tokenIndex, "?"))
            {
                return true;
            }

            if (_tokens[_tokenIndex].Value != "?")
            {
                return false;
            }

            if (_tokenIndex - 1 < 0)
            {
                return true;
            }

            PatternToken previous = GetSafeToken(_tokenIndex - 1);
            return previous.Type is not (PatternTokenType.Name or PatternTokenType.Regexp or PatternTokenType.Close or PatternTokenType.Asterisk);
        }

        private bool IsHashPrefix() => IsNonSpecialPatternChar(_tokenIndex, "#");

        private string MakeComponentString()
        {
            PatternToken token = _tokens[_tokenIndex];
            int componentStartIndex = GetSafeToken(_componentStart).Index;
            return _input[componentStartIndex..token.Index];
        }

        private void ComputeProtocolMatchesSpecialSchemeFlag()
        {
            URLPatternComponent protocol = URLPatternComponent.Compile(MakeComponentString(), URLPatternCanonicalizer.Protocol, PatternOptions.Default);
            _protocolMatchesSpecialScheme = URLPattern.ProtocolComponentMatchesSpecialScheme(protocol);
        }
    }
}
