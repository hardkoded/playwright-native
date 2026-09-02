/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Wraps a page-evaluate function plus argument as a single expression.
    /// </summary>
    internal static class EvaluateWithArg
    {
        /// <summary>
        /// Official error when <c>frame.evaluate</c> runs on a detached frame.
        /// </summary>
        internal const string FrameDetachedMessage = "frame.evaluate: Frame was detached";

        /// <summary>
        /// Official error when an element handle cannot be adopted into another document.
        /// </summary>
        internal const string UnableToAdoptMessage = "Unable to adopt element handle from a different document";

        /// <summary>
        /// Builds <c>(expression)(argJson)</c> so browsers that only evaluate expressions
        /// can still pass a JSON-serializable argument.
        /// </summary>
        /// <param name="expression">A JavaScript function (e.g. <c>x =&gt; x + 1</c>).</param>
        /// <param name="arg">The argument to serialize.</param>
        /// <param name="throwOnFunctions">
        /// When <see langword="true"/>, delegates in <paramref name="arg"/> throw
        /// the official evaluate serialize error. Init scripts pass
        /// <see langword="false"/> so functions are dropped.
        /// </param>
        /// <returns>An evaluable JavaScript expression.</returns>
        internal static string Wrap(string expression, object arg, bool throwOnFunctions = true)
        {
            if (throwOnFunctions)
            {
                EvaluateCallbacks.ThrowIfHasFunctions(arg);
            }

            string tagged = EvaluateSerialization.SerializeCallArgument(arg).GetRawText();
            return "(" + expression + ")((" + EvaluateSerialization.ParseJs + ")(" + tagged + "))";
        }

        /// <summary>
        /// Throws when <paramref name="frame"/> has been detached.
        /// </summary>
        /// <param name="frame">The frame that will evaluate.</param>
        internal static void ThrowIfDetached(IFrame frame)
        {
            if (frame != null && frame.IsDetached)
            {
                throw new PlaywrightSharpException(FrameDetachedMessage);
            }
        }

        /// <summary>
        /// Returns whether <paramref name="arg"/> is a live JS handle that must be
        /// passed by remote object id (not JSON).
        /// </summary>
        /// <param name="arg">The evaluate argument.</param>
        /// <returns><see langword="true"/> when the argument is a JS handle.</returns>
        internal static bool IsHandle(object arg) => arg is IJSHandle;

        /// <summary>
        /// Invokes function-like expressions so <c>() =&gt; document.body</c> evaluates to
        /// the body node rather than the function object. Matches Playwright's
        /// evaluate-handle string convention.
        /// </summary>
        /// <param name="expression">A JavaScript expression or function.</param>
        /// <returns>The expression, or <c>(function)()</c> when it looks like a function.</returns>
        internal static string InvokeIfFunction(string expression)
        {
            if (!IsFunction(expression))
            {
                return expression;
            }

            return "(" + expression + ")()";
        }

        /// <summary>
        /// Converts a handle-evaluate string into a function declaration.
        /// Function-like expressions are used as-is; other expressions become
        /// <c>() =&gt; (expression)</c> so <c>Runtime.callFunctionOn</c> can evaluate them.
        /// </summary>
        /// <param name="expression">A JavaScript expression or function.</param>
        /// <returns>A function declaration suitable for handle evaluation.</returns>
        internal static string AsFunction(string expression)
        {
            if (IsFunction(expression))
            {
                return expression;
            }

            return "() => (" + expression + ")";
        }

        /// <summary>
        /// Returns whether <paramref name="expression"/> looks like a JavaScript function
        /// (arrow, <c>function</c>, or <c>async</c>).
        /// </summary>
        /// <param name="expression">The expression to inspect.</param>
        /// <returns><see langword="true"/> when the expression should be invoked.</returns>
        internal static bool IsFunction(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return false;
            }

            string trimmed = expression.TrimStart();
            if (trimmed.StartsWith("function", StringComparison.Ordinal)
                || trimmed.StartsWith("async function", StringComparison.Ordinal)
                || trimmed.StartsWith("async ", StringComparison.Ordinal)
                || trimmed.StartsWith("async(", StringComparison.Ordinal))
            {
                return true;
            }

            return HasTopLevelArrow(trimmed);
        }

        private static bool HasTopLevelArrow(string trimmed)
        {
            int depth = 0;
            bool inSingle = false;
            bool inDouble = false;
            bool escape = false;
            for (int i = 0; i < trimmed.Length - 1; i++)
            {
                char c = trimmed[i];
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (inSingle)
                {
                    if (c == '\'')
                    {
                        inSingle = false;
                    }

                    continue;
                }

                if (inDouble)
                {
                    if (c == '"')
                    {
                        inDouble = false;
                    }

                    continue;
                }

                if (c == '\'')
                {
                    inSingle = true;
                    continue;
                }

                if (c == '"')
                {
                    inDouble = true;
                    continue;
                }

                if (c == '(' || c == '{' || c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == ')' || c == '}' || c == ']')
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && c == '.')
                {
                    return false;
                }

                if (depth == 0 && c == '=' && trimmed[i + 1] == '>')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
