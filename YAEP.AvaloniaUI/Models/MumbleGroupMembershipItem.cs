using CommunityToolkit.Mvvm.ComponentModel;

namespace YAEP.Models
{
    /// <summary>
    /// Represents a server group with a checkbox for edit-link "which groups this link belongs to".
    /// </summary>
    public partial class MumbleGroupMembershipItem : ObservableObject
    {
        public long GroupId { get; }
        public string GroupName { get; }

        [ObservableProperty]
        private bool _isInGroup;

        public MumbleGroupMembershipItem(long groupId, string groupName, bool isInGroup)
        {
            GroupId = groupId;
            GroupName = groupName ?? string.Empty;
            IsInGroup = isInGroup;
        }
    }
}
