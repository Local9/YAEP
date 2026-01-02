namespace YAEP.Models
{
    /// <summary>
    /// Represents a profile in the database.
    /// </summary>
    public class Profile
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? DeletedAt { get; set; }
        public bool IsActive { get; set; }
        public string SwitchHotkey { get; set; } = string.Empty;

        public bool IsDeleted => DeletedAt.HasValue;
    }
}

