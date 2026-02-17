using System;
using System.Diagnostics;
using Verse;

namespace Better_Architect_Edit_mode
{
    public static class Logger
    {
        [Conditional("DEBUG")]
        public static void Message(string message)
        {
            Log.Message("[BetterArchitectEditMode] " + message);
        }

        [Conditional("DEBUG")]
        public static void Warning(string message)
        {
            Log.Warning("[BetterArchitectEditMode] " + message);
        }

        [Conditional("DEBUG")]
        public static void Error(string message)
        {
            Log.Error("[BetterArchitectEditMode] " + message);
        }

        [Conditional("DEBUG")]
        public static void Exception(Exception exception, string context = null)
        {
            if (exception == null)
            {
                return;
            }

            var prefix = string.IsNullOrWhiteSpace(context)
                ? "[BetterArchitectEditMode] "
                : "[BetterArchitectEditMode] " + context + ": ";
            Log.Error(prefix + exception);
        }
    }
}
