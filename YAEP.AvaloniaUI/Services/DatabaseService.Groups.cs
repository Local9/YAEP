using Microsoft.Data.Sqlite;

namespace YAEP.Services
{
    /// <summary>
    /// Client group management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
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

        /// <summary>
        /// Represents a client group member (a window title in a group).
        /// </summary>
        public class ClientGroupMember
        {
            public long GroupId { get; set; }
            public string WindowTitle { get; set; } = string.Empty;
            public int DisplayOrder { get; set; }
        }

        /// <summary>
        /// Represents a client group with its members.
        /// </summary>
        public class ClientGroupWithMembers
        {
            public ClientGroup Group { get; set; } = new();
            public List<ClientGroupMember> Members { get; set; } = new();
        }

        /// <summary>
        /// Checks if a group is the default group (lowest ID for the profile).
        /// </summary>
        /// <param name="groupId">The group ID to check.</param>
        /// <returns>True if the group is the default group, false otherwise.</returns>
        public bool IsDefaultGroup(long groupId)
        {
            object? profileIdObj = ExecuteScalar("SELECT ProfileId FROM ClientGroups WHERE Id = $groupId",
                cmd => cmd.Parameters.AddWithValue("$groupId", groupId));
            
            if (profileIdObj == null)
            {
                return false;
            }

            long profileId = Convert.ToInt64(profileIdObj);

            object? minId = ExecuteScalar("SELECT MIN(Id) FROM ClientGroups WHERE ProfileId = $profileId",
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));
            
            return minId != null && Convert.ToInt64(minId) == groupId;
        }

        /// <summary>
        /// Gets all client groups for a specific profile, ordered by DisplayOrder.
        /// </summary>
        public List<ClientGroup> GetClientGroups(long profileId)
        {
            List<ClientGroup> groups = new List<ClientGroup>();

            ExecuteReader("SELECT Id, ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey FROM ClientGroups WHERE ProfileId = $profileId ORDER BY DisplayOrder, Id",
                reader => groups.Add(new ClientGroup
                {
                    Id = reader.GetInt64(0),
                    ProfileId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                    CycleForwardHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    CycleBackwardHotkey = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                }),
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

            return groups;
        }

        /// <summary>
        /// Gets all client groups with their members for a specific profile.
        /// </summary>
        public List<ClientGroupWithMembers> GetClientGroupsWithMembers(long profileId)
        {
            List<ClientGroupWithMembers> groupsWithMembers = new List<ClientGroupWithMembers>();

            List<ClientGroup> groups = GetClientGroups(profileId);
            foreach (ClientGroup group in groups)
            {
                List<ClientGroupMember> members = GetClientGroupMembers(group.Id);
                groupsWithMembers.Add(new ClientGroupWithMembers
                {
                    Group = group,
                    Members = members
                });
            }

            return groupsWithMembers;
        }

        /// <summary>
        /// Gets all members of a specific client group, ordered by DisplayOrder.
        /// </summary>
        public List<ClientGroupMember> GetClientGroupMembers(long groupId)
        {
            List<ClientGroupMember> members = new List<ClientGroupMember>();

            ExecuteReader("SELECT GroupId, WindowTitle, DisplayOrder FROM ClientGroupMembers WHERE GroupId = $groupId ORDER BY DisplayOrder, WindowTitle",
                reader => members.Add(new ClientGroupMember
                {
                    GroupId = reader.GetInt64(0),
                    WindowTitle = reader.GetString(1),
                    DisplayOrder = reader.GetInt32(2)
                }),
                cmd => cmd.Parameters.AddWithValue("$groupId", groupId));

            return members;
        }

        /// <summary>
        /// Creates a new client group.
        /// </summary>
        public ClientGroup? CreateClientGroup(long profileId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            int maxOrder = Convert.ToInt32(ExecuteScalar("SELECT COALESCE(MAX(DisplayOrder), -1) FROM ClientGroups WHERE ProfileId = $profileId",
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId)) ?? -1);
            int newOrder = maxOrder + 1;

            try
            {
                ExecuteNonQuery(@"
                    INSERT INTO ClientGroups (ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey)
                    VALUES ($profileId, $name, $displayOrder, '', '')",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$profileId", profileId);
                        cmd.Parameters.AddWithValue("$name", name.Trim());
                        cmd.Parameters.AddWithValue("$displayOrder", newOrder);
                    });

                ClientGroup? group = null;
                ExecuteReader("SELECT Id, ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey FROM ClientGroups WHERE Id = last_insert_rowid()",
                    reader =>
                    {
                        if (group == null)
                        {
                            group = new ClientGroup
                            {
                                Id = reader.GetInt64(0),
                                ProfileId = reader.GetInt64(1),
                                Name = reader.GetString(2),
                                DisplayOrder = reader.GetInt32(3),
                                CycleForwardHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                CycleBackwardHotkey = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                            };
                        }
                    });

                return group;
            }
            catch (SqliteException)
            {
                return null;
            }
        }

        /// <summary>
        /// Updates a client group's name.
        /// </summary>
        public void UpdateClientGroupName(long groupId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            ExecuteNonQuery("UPDATE ClientGroups SET Name = $name WHERE Id = $groupId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                    cmd.Parameters.AddWithValue("$name", name.Trim());
                });
        }

        /// <summary>
        /// Updates the display order of client groups.
        /// </summary>
        public void UpdateClientGroupOrder(long profileId, List<long> groupIdsInOrder)
        {
            ExecuteTransaction(connection =>
            {
                for (int i = 0; i < groupIdsInOrder.Count; i++)
                {
                    ExecuteNonQuery(connection, "UPDATE ClientGroups SET DisplayOrder = $displayOrder WHERE Id = $groupId AND ProfileId = $profileId",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$groupId", groupIdsInOrder[i]);
                            cmd.Parameters.AddWithValue("$displayOrder", i);
                            cmd.Parameters.AddWithValue("$profileId", profileId);
                        });
                }
            });
        }

        /// <summary>
        /// Deletes a client group and all its members.
        /// </summary>
        public void DeleteClientGroup(long groupId)
        {
            object? profileIdObj = ExecuteScalar("SELECT ProfileId FROM ClientGroups WHERE Id = $groupId",
                cmd => cmd.Parameters.AddWithValue("$groupId", groupId));
            
            if (profileIdObj == null)
            {
                return;
            }

            long profileId = Convert.ToInt64(profileIdObj);

            object? minId = ExecuteScalar("SELECT MIN(Id) FROM ClientGroups WHERE ProfileId = $profileId",
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));
            
            if (minId != null && Convert.ToInt64(minId) == groupId)
            {
                return;
            }

            ExecuteNonQuery("DELETE FROM ClientGroups WHERE Id = $groupId",
                cmd => cmd.Parameters.AddWithValue("$groupId", groupId));
        }

        /// <summary>
        /// Adds a client (window title) to a group.
        /// </summary>
        public void AddClientToGroup(long groupId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            long exists = (long)(ExecuteScalar("SELECT COUNT(*) FROM ClientGroupMembers WHERE GroupId = $groupId AND WindowTitle = $windowTitle",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                    cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                }) ?? 0L);

            if (exists > 0)
                return;

            int maxOrder = Convert.ToInt32(ExecuteScalar("SELECT COALESCE(MAX(DisplayOrder), -1) FROM ClientGroupMembers WHERE GroupId = $groupId",
                cmd => cmd.Parameters.AddWithValue("$groupId", groupId)) ?? -1);
            int newOrder = maxOrder + 1;

            ExecuteNonQuery(@"
                INSERT INTO ClientGroupMembers (GroupId, WindowTitle, DisplayOrder)
                VALUES ($groupId, $windowTitle, $displayOrder)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                    cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                    cmd.Parameters.AddWithValue("$displayOrder", newOrder);
                });
        }

        /// <summary>
        /// Removes a client (window title) from a group.
        /// </summary>
        public void RemoveClientFromGroup(long groupId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            ExecuteNonQuery("DELETE FROM ClientGroupMembers WHERE GroupId = $groupId AND WindowTitle = $windowTitle",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                    cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                });
        }

        /// <summary>
        /// Updates the display order of clients within a group.
        /// </summary>
        public void UpdateClientGroupMemberOrder(long groupId, List<string> windowTitlesInOrder)
        {
            ExecuteTransaction(connection =>
            {
                for (int i = 0; i < windowTitlesInOrder.Count; i++)
                {
                    ExecuteNonQuery(connection, "UPDATE ClientGroupMembers SET DisplayOrder = $displayOrder WHERE GroupId = $groupId AND WindowTitle = $windowTitle",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$groupId", groupId);
                            cmd.Parameters.AddWithValue("$windowTitle", windowTitlesInOrder[i].Trim());
                            cmd.Parameters.AddWithValue("$displayOrder", i);
                        });
                }
            });
        }

        /// <summary>
        /// Gets all window titles that are not in any group for a specific profile.
        /// </summary>
        public List<string> GetUngroupedClients(long profileId)
        {
            HashSet<string> allClients = new HashSet<string>();
            ExecuteReader("SELECT DISTINCT WindowTitle FROM ThumbnailSettings WHERE ProfileId = $profileId",
                reader => allClients.Add(reader.GetString(0)),
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

            HashSet<string> grouped = new HashSet<string>();
            ExecuteReader(@"
                SELECT DISTINCT cgm.WindowTitle
                FROM ClientGroupMembers cgm
                INNER JOIN ClientGroups cg ON cgm.GroupId = cg.Id
                WHERE cg.ProfileId = $profileId",
                reader => grouped.Add(reader.GetString(0)),
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

            return allClients.Except(grouped).ToList();
        }

        /// <summary>
        /// Updates the hotkeys for a client group.
        /// </summary>
        public void UpdateClientGroupHotkeys(long groupId, string forwardHotkey, string backwardHotkey)
        {
            ExecuteNonQuery(@"
                UPDATE ClientGroups 
                SET CycleForwardHotkey = $forwardHotkey, CycleBackwardHotkey = $backwardHotkey
                WHERE Id = $groupId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                    cmd.Parameters.AddWithValue("$forwardHotkey", forwardHotkey ?? string.Empty);
                    cmd.Parameters.AddWithValue("$backwardHotkey", backwardHotkey ?? string.Empty);
                });
        }

        /// <summary>
        /// Gets all client group members for a specific profile, ordered by group DisplayOrder and then member DisplayOrder.
        /// Returns a dictionary mapping WindowTitle to a tuple of (GroupDisplayOrder, MemberDisplayOrder).
        /// </summary>
        public Dictionary<string, (int GroupDisplayOrder, int MemberDisplayOrder)> GetAllClientGroupMembersForProfile(long profileId)
        {
            Dictionary<string, (int, int)> result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

            List<ClientGroupWithMembers> groupsWithMembers = GetClientGroupsWithMembers(profileId);
            List<ClientGroupWithMembers> orderedGroups = groupsWithMembers.OrderBy(g => g.Group.DisplayOrder).ToList();

            foreach (ClientGroupWithMembers? groupWithMembers in orderedGroups)
            {
                List<ClientGroupMember> orderedMembers = groupWithMembers.Members.OrderBy(m => m.DisplayOrder).ToList();
                foreach (ClientGroupMember? member in orderedMembers)
                {
                    if (!result.ContainsKey(member.WindowTitle))
                    {
                        result[member.WindowTitle] = (groupWithMembers.Group.DisplayOrder, member.DisplayOrder);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the next client in the current group cycle for a profile.
        /// Returns the next window title to activate, or null if no groups/clients found.
        /// </summary>
        public string? GetNextClientInCycle(long profileId, bool forward = true)
        {
            List<ClientGroupWithMembers> groups = GetClientGroupsWithMembers(profileId);
            if (groups.Count == 0)
                return null;

            List<ClientGroupWithMembers> orderedGroups = groups.OrderBy(g => g.Group.DisplayOrder).ToList();

            foreach (ClientGroupWithMembers? group in orderedGroups)
            {
                List<ClientGroupMember> orderedMembers = group.Members.OrderBy(m => m.DisplayOrder).ToList();
                if (orderedMembers.Count > 0)
                {
                    return orderedMembers[0].WindowTitle;
                }
            }

            return null;
        }
    }
}

