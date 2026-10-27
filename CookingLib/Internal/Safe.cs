using System;
using System.Collections.Generic;

namespace CookingLib
{
    internal static class Safe
    {
        static readonly HashSet<string> logged = new HashSet<string>();

        internal static int Failures { get; private set; }

        internal static void Fail(string name, Exception e)
        {
            Failures++;
            if (logged.Add(name))
                Log.Error(name + " failed, the game carries on without it: " + e);
        }

        internal static bool Run(string name, Action body)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception e)
            {
                Fail(name, e);
                return false;
            }
        }

        internal static T Run<T>(string name, Func<T> body, T fallback)
        {
            try
            {
                return body();
            }
            catch (Exception e)
            {
                Fail(name, e);
                return fallback;
            }
        }
    }
}
