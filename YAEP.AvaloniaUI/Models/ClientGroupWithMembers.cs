namespace YAEP.Models
{
    /// <summary>
    /// Represents a client group with its members.
    /// </summary>
    public class ClientGroupWithMembers
    {
        public ClientGroup Group { get; set; } = new();
        public List<ClientGroupMember> Members { get; set; } = new();
    }
}

