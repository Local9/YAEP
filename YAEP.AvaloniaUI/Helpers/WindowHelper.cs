using System.ComponentModel;

namespace YAEP.Helpers
{
    /// <summary>
    /// Provides helper methods for window title validation, filtering, and retrieval.
    /// </summary>
    public static class WindowHelper
    {
        /// <summary>
        /// Determines if a window title should be included for thumbnail operations.
        /// Excludes "EVE" without hyphen - only includes "EVE - CharacterName" format.
        /// </summary>
        /// <param name="windowTitle">The window title to check.</param>
        /// <returns>True if the window title should be included, false otherwise.</returns>
        public static bool ShouldIncludeWindowTitle(string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return false;

            if (windowTitle.Equals("EVE", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Determines if a window title is the base "EVE" window (without character name).
        /// </summary>
        /// <param name="windowTitle">The window title to check.</param>
        /// <returns>True if the window title is "EVE", false otherwise.</returns>
        public static bool IsEveWindowTitle(string? windowTitle)
        {
            return !string.IsNullOrWhiteSpace(windowTitle) &&
                   windowTitle.Equals("EVE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Safely retrieves the main window title from a process, returning an empty string on error.
        /// </summary>
        /// <param name="process">The process to get the window title from.</param>
        /// <returns>The main window title, or an empty string if unavailable.</returns>
        public static string GetWindowTitle(Process process)
        {
            try
            {
                return process.MainWindowTitle;
            }
            catch (Win32Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Safely retrieves the main window title from a process, with a fallback value on error.
        /// </summary>
        /// <param name="process">The process to get the window title from.</param>
        /// <param name="fallback">The fallback value to return if the window title cannot be retrieved.</param>
        /// <returns>The main window title, or the fallback value if unavailable.</returns>
        public static string GetWindowTitle(Process process, string fallback)
        {
            try
            {
                return process.MainWindowTitle;
            }
            catch (Win32Exception)
            {
                return fallback;
            }
        }
    }
}

