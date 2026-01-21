namespace YAEP.Models
{
    /// <summary>
    /// Represents a character found in an EVE Online profile (from core_char_* files).
    /// </summary>
    public class EveOnlineCharacter
    {
        public string CharacterId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ProfilePath { get; set; } = string.Empty;
        public DateTime FileModifiedDate { get; set; }
    }
}
