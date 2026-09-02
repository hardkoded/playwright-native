/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Tracks page event listeners and in-flight async handler tasks so
    /// official <c>page.removeAllListeners(type, { behavior })</c> can wait
    /// or ignore errors. Mirrors Playwright's client EventEmitter.
    /// </summary>
    internal sealed class PageListenerRegistry
    {
        internal TrackedEvent<IConsoleMessage> Console { get; } = new();

        internal static bool IsConsoleEvent(string type)
            => string.Equals(type, "console", StringComparison.OrdinalIgnoreCase);

        internal static RemoveAllListenersBehavior ParseBehavior(string behavior)
        {
            if (string.IsNullOrEmpty(behavior)
                || string.Equals(behavior, "default", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.Default;
            }

            if (string.Equals(behavior, "wait", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.Wait;
            }

            if (string.Equals(behavior, "ignoreErrors", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.IgnoreErrors;
            }

            throw new ArgumentException(
                $"Unknown removeAllListeners behavior '{behavior}'.",
                nameof(behavior));
        }

        /// <summary>
        /// Official <c>page.removeAllListeners</c>.
        /// </summary>
        /// <param name="type">Event name, or <see langword="null"/> for every tracked event.</param>
        /// <param name="behavior">Wait / ignoreErrors / default.</param>
        /// <returns>A task that completes when removal (and optional wait) is done.</returns>
        internal Task RemoveAllListenersAsync(string type, RemoveAllListenersBehavior behavior)
        {
            bool all = string.IsNullOrEmpty(type);
            bool console = all || IsConsoleEvent(type);
            if (console)
            {
                Console.RemoveAll();
            }

            if (behavior == RemoveAllListenersBehavior.IgnoreErrors)
            {
                if (console)
                {
                    Console.IgnoreSubsequentErrors();
                }

                return Task.CompletedTask;
            }

            if (behavior == RemoveAllListenersBehavior.Wait)
            {
                return console ? Console.WaitAsync() : Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// One multicast event plus its in-flight handler tasks.
        /// </summary>
        /// <typeparam name="T">Event argument type.</typeparam>
        internal sealed class TrackedEvent<T>
        {
            private readonly object _lock = new();
            private readonly HashSet<Task> _pending = new();
            private EventHandler<T> _handlers;
            private Action<Exception> _rejectionHandler;

            internal void Add(EventHandler<T> handler)
            {
                if (handler == null)
                {
                    throw new ArgumentNullException(nameof(handler));
                }

                lock (_lock)
                {
                    _handlers += handler;
                }
            }

            internal void Remove(EventHandler<T> handler)
            {
                if (handler == null)
                {
                    return;
                }

                lock (_lock)
                {
                    _handlers -= handler;
                }
            }

            internal void RemoveAll()
            {
                lock (_lock)
                {
                    _handlers = null;
                }
            }

            internal void IgnoreSubsequentErrors()
            {
                lock (_lock)
                {
                    _rejectionHandler = _ => { };
                }
            }

            internal void Emit(object sender, T args)
            {
                EventHandler<T> handlers;
                lock (_lock)
                {
                    handlers = _handlers;
                }

                if (handlers == null)
                {
                    return;
                }

                foreach (Delegate item in handlers.GetInvocationList())
                {
                    EventHandler<T> handler = (EventHandler<T>)item;
                    Track(InvokeHandlerAsync(handler, sender, args));
                }
            }

            internal async Task WaitAsync()
            {
                List<Exception> errors = new();
                lock (_lock)
                {
                    _rejectionHandler = error => errors.Add(error);
                }

                Task[] pending;
                lock (_lock)
                {
                    pending = new Task[_pending.Count];
                    _pending.CopyTo(pending);
                }

                foreach (Task task in pending)
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Unwrap(ex));
                    }
                }

                if (errors.Count > 0)
                {
                    throw errors[0];
                }
            }

            private static Task InvokeHandlerAsync(EventHandler<T> handler, object sender, T args)
            {
                AsyncVoidTrackingSynchronizationContext context = new();
                SynchronizationContext previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    handler.Invoke(sender, args);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                    context.FinishInvoke();
                }

                return context.Task;
            }

            private static Exception Unwrap(Exception ex)
            {
                if (ex is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
                {
                    return aggregate.InnerExceptions[0];
                }

                return ex.InnerException ?? ex;
            }

            private void Track(Task task)
            {
                if (task.IsCompletedSuccessfully)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    NotifyRejection(Unwrap(task.Exception));
                    return;
                }

                lock (_lock)
                {
                    _pending.Add(task);
                }

                _ = ObserveAsync(task);
            }

            private async Task ObserveAsync(Task task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    NotifyRejection(Unwrap(ex));
                }
                finally
                {
                    lock (_lock)
                    {
                        _pending.Remove(task);
                    }
                }
            }

            private void NotifyRejection(Exception error)
            {
                Action<Exception> handler;
                lock (_lock)
                {
                    handler = _rejectionHandler;
                }

                handler?.Invoke(error);
            }

            private sealed class AsyncVoidTrackingSynchronizationContext : SynchronizationContext
            {
                private readonly TaskCompletionSource<bool> _completion =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);

                private int _pendingOperations = 1;
                private Exception _exception;

                internal Task Task => _completion.Task;

                public override SynchronizationContext CreateCopy() => this;

                public override void OperationStarted()
                    => Interlocked.Increment(ref _pendingOperations);

                public override void OperationCompleted()
                    => TryComplete();

                public override void Post(SendOrPostCallback d, object state)
                    => Run(d, state);

                public override void Send(SendOrPostCallback d, object state)
                    => Run(d, state);

                internal void FinishInvoke() => TryComplete();

                private void Run(SendOrPostCallback d, object state)
                {
                    try
                    {
                        d(state);
                    }
                    catch (Exception ex)
                    {
                        _exception = ex;
                    }
                }

                private void TryComplete()
                {
                    if (Interlocked.Decrement(ref _pendingOperations) != 0)
                    {
                        return;
                    }

                    if (_exception != null)
                    {
                        _completion.TrySetException(_exception);
                    }
                    else
                    {
                        _completion.TrySetResult(true);
                    }
                }
            }
        }
    }
}
