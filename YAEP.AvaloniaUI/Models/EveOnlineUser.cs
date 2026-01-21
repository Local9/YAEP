namespace YAEP.Models
{
    /// <summary>
    /// Represents a user found in an EVE Online profile (from core_user_* files).
    /// </summary>
    public class EveOnlineUser
    {
        public string UserId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ProfilePath { get; set; } = string.Empty;
        public DateTime FileModifiedDate { get; set; }
    }
}
