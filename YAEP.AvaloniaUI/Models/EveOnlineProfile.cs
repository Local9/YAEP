namespace YAEP.Models
{
    /// <summary>
    /// Represents an EVE Online client profile (settings folder).
    /// </summary>
    public class EveOnlineProfile
    {
        public string ServerName { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}
