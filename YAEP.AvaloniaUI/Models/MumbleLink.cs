using YAEP.Helpers;

namespace YAEP.Models
{
    /// <summary>
    /// Represents a Mumble link in the database.
    /// </summary>
    public class MumbleLink
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsSelected { get; set; }
        public long? ServerGroupId { get; set; }
        /// <summary>Display name of the server group when loaded from DB (optional, may be empty).</summary>
        public string ServerGroupName { get; set; } = string.Empty;
        public string Hotkey { get; set; } = string.Empty;

        public void OpenLink()
        {
            OpenLink(Url);
        }

        /// <summary>
        /// Opens a Mumble link URL.
        /// </summary>
        /// <param name="url">The URL to open. If null, uses this instance's Url property.</param>
        public static void OpenLink(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !SecurityValidationHelper.IsValidMumbleUrl(url))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid or unsafe URL format: {url}");
                return;
            }

            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open Mumble link: {ex.Message}");
            }
        }
    }
}

