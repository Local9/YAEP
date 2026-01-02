namespace YAEP.Models
{
    /// <summary>
    /// Represents a client group member (a window title in a group).
    /// </summary>
    public class ClientGroupMember
    {
        public long GroupId { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}

