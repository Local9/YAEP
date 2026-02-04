namespace YAEP.Models
{
    /// <summary>
    /// Represents a Mumble server group (e.g. a named server) that links can be grouped under.
    /// </summary>
    public class MumbleServerGroup
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
