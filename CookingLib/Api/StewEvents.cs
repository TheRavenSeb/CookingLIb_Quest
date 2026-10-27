using System;

namespace CookingLib
{
    /// <summary>Events for stews in pots. They fire on the server only.</summary>
    public static class StewEvents
    {
        /// <summary>A pot locked its stew: it checked a full set of ingredients, or the stew cooked or burnt before that. Handlers run on the main thread from the next frame.</summary>
        public static event Action<StewEvent> Made;

        /// <summary>A stew in a pot cooked, burnt or burnt away. When one change also locks the pot, Made comes first. Handlers run on the main thread from the next frame.</summary>
        public static event Action<StewEvent> StageChanged;

        /// <summary>Why the events are off, or null when they work. Always set on players' machines.</summary>
        public static string Error
        {
            get
            {
                if (!Main.IsServer)
                    return "stew events run on the server only";
                if (!ServerPatches.StewEventsOn)
                    return "the stew event patches failed to apply, see the log";
                return StewStages.Error;
            }
        }

        internal static int MadeFired { get; private set; }

        internal static int StageChangedFired { get; private set; }

        internal static int MadeHandlers => Made?.GetInvocationList().Length ?? 0;

        internal static int StageChangedHandlers => StageChanged?.GetInvocationList().Length ?? 0;

        internal static bool WantsMade => Made != null && Error == null;

        internal static bool WantsStageChanged => StageChanged != null && Error == null;

        internal static bool Wanted => WantsMade || WantsStageChanged;

        internal static void QueueMade(StewEvent args)
        {
            MadeFired++;
            EventQueue.Enqueue(Made, args, nameof(Made));
        }

        internal static void QueueStageChanged(StewEvent args)
        {
            StageChangedFired++;
            EventQueue.Enqueue(StageChanged, args, nameof(StageChanged));
        }
    }
}
