using System.IO;
using System.Text.RegularExpressions;

namespace YAEP.Helpers
{
    /// <summary>
    /// Provides security validation methods for user inputs.
    /// </summary>
    public static class SecurityValidationHelper
    {
        /// <summary>
        /// Validates if a Mumble URL is safe to store and execute.
        /// </summary>
        /// <param name="url">The Mumble URL to validate.</param>
        /// <returns>True if the URL is a valid Mumble URL, false otherwise.</returns>
        public static bool IsValidMumbleUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.Length > 2048)
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return false;

            if (!uri.Scheme.Equals("mumble", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrEmpty(uri.Host))
                return false;

            if (url.Contains(';') || url.Contains('|') || url.Contains('`'))
                return false;

            return true;
        }

        /// <summary>
        /// Validates if a process name is safe to use with Process.GetProcessesByName.
        /// </summary>
        /// <param name="processName">The process name to validate.</param>
        /// <returns>True if the process name is safe, false otherwise.</returns>
        public static bool IsValidProcessName(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            string name = processName;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            if (!Regex.IsMatch(name, @"^[a-zA-Z0-9_.-]+$"))
                return false;

            if (name.Length > 260)
                return false;

            if (name.Contains("..") || name.Contains("/") || name.Contains("\\"))
                return false;

            return true;
        }

        /// <summary>
        /// Validates and normalizes a file system path to prevent path traversal attacks.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>The normalized absolute path if valid.</returns>
        /// <exception cref="ArgumentException">Thrown if the path is invalid or contains traversal sequences.</exception>
        public static string ValidateAndNormalizePath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            try
            {
                string fullPath = Path.GetFullPath(path);

                if (fullPath.Contains(".."))
                {
                    string normalized = Path.GetFullPath(fullPath);
                    if (normalized.Contains(".."))
                        throw new ArgumentException("Path contains invalid traversal sequences", nameof(path));
                }

                return fullPath;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
            {
                throw new ArgumentException($"Invalid path: {ex.Message}", nameof(path), ex);
            }
        }

        /// <summary>
        /// Validates an assembly name to prevent path traversal and malicious loading.
        /// </summary>
        /// <param name="assemblyName">The assembly name to validate.</param>
        /// <returns>True if the assembly name is safe, false otherwise.</returns>
        public static bool IsValidAssemblyName(string? assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            if (assemblyName.Contains("..") || assemblyName.Contains("/") || assemblyName.Contains("\\"))
                return false;

            if (!Regex.IsMatch(assemblyName, @"^[a-zA-Z0-9_.-]+$"))
                return false;

            if (assemblyName.Length > 260)
                return false;

            return true;
        }
    }
}

