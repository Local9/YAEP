namespace YAEP.Models
{
    /// <summary>
    /// Represents a client group.
    /// </summary>
    public class ClientGroup
    {
        public long Id { get; set; }
        public long ProfileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public string CycleForwardHotkey { get; set; } = string.Empty;
        public string CycleBackwardHotkey { get; set; } = string.Empty;
    }
}

