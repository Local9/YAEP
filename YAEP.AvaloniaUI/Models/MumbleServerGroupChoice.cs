namespace YAEP.Models
{
    /// <summary>
    /// Represents a selectable server group option for dropdowns (e.g. "No group" with null Id, or a real group).
    /// </summary>
    public class MumbleServerGroupChoice
    {
        public long? Id { get; }
        public string Name { get; }

        public MumbleServerGroupChoice(long? id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }
    }
}
