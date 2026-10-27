using System;
using System.Collections.Generic;
using System.Reflection;

namespace CookingLib
{
    internal static class EventQueue
    {
        static readonly Queue<Action> pending = new Queue<Action>();
        static readonly HashSet<MethodInfo> loggedChecks = new HashSet<MethodInfo>();

        internal static int HandlerFailures { get; private set; }

        internal static void Enqueue<T>(Action<T> handlers, T args, string name)
        {
            if (handlers != null)
                pending.Enqueue(() => Fire(handlers, args, name));
        }

        internal static void Drain()
        {
            for (int count = pending.Count; count > 0; count--)
                pending.Dequeue()();
        }

        internal static void Fire<T>(Action<T> handlers, T args, string name)
        {
            if (handlers == null)
                return;

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(args);
                }
                catch (Exception e)
                {
                    Failed(name, handler, e);
                }
            }
        }

        internal static void Fire(Action handlers, string name)
        {
            if (handlers == null)
                return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Failed(name, handler, e);
                }
            }
        }

        internal static bool Check<T>(Func<T, bool> handlers, T args, string name)
        {
            if (handlers == null)
                return true;

            foreach (Func<T, bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    if (!handler(args))
                        return false;
                }
                catch (Exception e)
                {
                    if (loggedChecks.Add(handler.Method))
                        Failed(name, handler, e);
                    else
                        HandlerFailures++;
                }
            }
            return true;
        }

        static void Failed(string name, Delegate handler, Exception e)
        {
            HandlerFailures++;
            Log.Error(name + " handler " + handler.Method.DeclaringType?.FullName + "." + handler.Method.Name + " failed, the others still run");
            Log.Error(e.ToString());
        }
    }
}
