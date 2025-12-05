using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;

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
        /// Initializes the client groups tables in the database.
        /// </summary>
        private void InitializeClientGroupsTables()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Create ClientGroups table if it doesn't exist
            SqliteCommand createGroupsTableCommand = connection.CreateCommand();
            createGroupsTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ClientGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProfileId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    CycleForwardHotkey TEXT NOT NULL DEFAULT '',
                    CycleBackwardHotkey TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE,
                    UNIQUE(ProfileId, Name)
                )";
            createGroupsTableCommand.ExecuteNonQuery();

            // Add hotkey columns if they don't exist (migration for existing databases)
            try
            {
                SqliteCommand addForwardHotkeyColumnCommand = connection.CreateCommand();
                addForwardHotkeyColumnCommand.CommandText = "ALTER TABLE ClientGroups ADD COLUMN CycleForwardHotkey TEXT NOT NULL DEFAULT ''";
                addForwardHotkeyColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists, ignore
            }

            try
            {
                SqliteCommand addBackwardHotkeyColumnCommand = connection.CreateCommand();
                addBackwardHotkeyColumnCommand.CommandText = "ALTER TABLE ClientGroups ADD COLUMN CycleBackwardHotkey TEXT NOT NULL DEFAULT ''";
                addBackwardHotkeyColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists, ignore
            }

            // Create ClientGroupMembers table if it doesn't exist
            SqliteCommand createMembersTableCommand = connection.CreateCommand();
            createMembersTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ClientGroupMembers (
                    GroupId INTEGER NOT NULL,
                    WindowTitle TEXT NOT NULL,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (GroupId, WindowTitle),
                    FOREIGN KEY (GroupId) REFERENCES ClientGroups(Id) ON DELETE CASCADE
                )";
            createMembersTableCommand.ExecuteNonQuery();

            connection.Close();
        }

        /// <summary>
        /// Gets all client groups for a specific profile, ordered by DisplayOrder.
        /// </summary>
        public List<ClientGroup> GetClientGroups(long profileId)
        {
            List<ClientGroup> groups = new List<ClientGroup>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey FROM ClientGroups WHERE ProfileId = $profileId ORDER BY DisplayOrder, Id";
            command.Parameters.AddWithValue("$profileId", profileId);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                groups.Add(new ClientGroup
                {
                    Id = reader.GetInt64(0),
                    ProfileId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                    CycleForwardHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    CycleBackwardHotkey = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                });
            }

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

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT GroupId, WindowTitle, DisplayOrder FROM ClientGroupMembers WHERE GroupId = $groupId ORDER BY DisplayOrder, WindowTitle";
            command.Parameters.AddWithValue("$groupId", groupId);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                members.Add(new ClientGroupMember
                {
                    GroupId = reader.GetInt64(0),
                    WindowTitle = reader.GetString(1),
                    DisplayOrder = reader.GetInt32(2)
                });
            }

            return members;
        }

        /// <summary>
        /// Creates a new client group.
        /// </summary>
        public ClientGroup? CreateClientGroup(long profileId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get the maximum display order for this profile
            SqliteCommand maxOrderCommand = connection.CreateCommand();
            maxOrderCommand.CommandText = "SELECT COALESCE(MAX(DisplayOrder), -1) FROM ClientGroups WHERE ProfileId = $profileId";
            maxOrderCommand.Parameters.AddWithValue("$profileId", profileId);
            int maxOrder = Convert.ToInt32(maxOrderCommand.ExecuteScalar() ?? -1);
            int newOrder = maxOrder + 1;

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ClientGroups (ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey)
                VALUES ($profileId, $name, $displayOrder, '', '')";
            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$name", name.Trim());
            command.Parameters.AddWithValue("$displayOrder", newOrder);

            try
            {
                command.ExecuteNonQuery();

                // Get the created group ID
                SqliteCommand getGroupCommand = connection.CreateCommand();
                getGroupCommand.CommandText = "SELECT Id, ProfileId, Name, DisplayOrder, CycleForwardHotkey, CycleBackwardHotkey FROM ClientGroups WHERE Id = last_insert_rowid()";

                using SqliteDataReader reader = getGroupCommand.ExecuteReader();
                if (reader.Read())
                {
                    return new ClientGroup
                    {
                        Id = reader.GetInt64(0),
                        ProfileId = reader.GetInt64(1),
                        Name = reader.GetString(2),
                        DisplayOrder = reader.GetInt32(3),
                        CycleForwardHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        CycleBackwardHotkey = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                    };
                }
            }
            catch (SqliteException)
            {
                // Group name already exists or other error
                return null;
            }

            return null;
        }

        /// <summary>
        /// Updates a client group's name.
        /// </summary>
        public void UpdateClientGroupName(long groupId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE ClientGroups SET Name = $name WHERE Id = $groupId";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$name", name.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Updates the display order of client groups.
        /// </summary>
        public void UpdateClientGroupOrder(long profileId, List<long> groupIdsInOrder)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                for (int i = 0; i < groupIdsInOrder.Count; i++)
                {
                    SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE ClientGroups SET DisplayOrder = $displayOrder WHERE Id = $groupId AND ProfileId = $profileId";
                    command.Parameters.AddWithValue("$groupId", groupIdsInOrder[i]);
                    command.Parameters.AddWithValue("$displayOrder", i);
                    command.Parameters.AddWithValue("$profileId", profileId);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Deletes a client group and all its members.
        /// </summary>
        public void DeleteClientGroup(long groupId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ClientGroups WHERE Id = $groupId";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Adds a client (window title) to a group.
        /// </summary>
        public void AddClientToGroup(long groupId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Check if already in group
            SqliteCommand checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM ClientGroupMembers WHERE GroupId = $groupId AND WindowTitle = $windowTitle";
            checkCommand.Parameters.AddWithValue("$groupId", groupId);
            checkCommand.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
            long exists = (long)checkCommand.ExecuteScalar()!;

            if (exists > 0)
                return; // Already in group

            // Get the maximum display order for this group
            SqliteCommand maxOrderCommand = connection.CreateCommand();
            maxOrderCommand.CommandText = "SELECT COALESCE(MAX(DisplayOrder), -1) FROM ClientGroupMembers WHERE GroupId = $groupId";
            maxOrderCommand.Parameters.AddWithValue("$groupId", groupId);
            int maxOrder = Convert.ToInt32(maxOrderCommand.ExecuteScalar() ?? -1);
            int newOrder = maxOrder + 1;

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ClientGroupMembers (GroupId, WindowTitle, DisplayOrder)
                VALUES ($groupId, $windowTitle, $displayOrder)";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
            command.Parameters.AddWithValue("$displayOrder", newOrder);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Removes a client (window title) from a group.
        /// </summary>
        public void RemoveClientFromGroup(long groupId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ClientGroupMembers WHERE GroupId = $groupId AND WindowTitle = $windowTitle";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Updates the display order of clients within a group.
        /// </summary>
        public void UpdateClientGroupMemberOrder(long groupId, List<string> windowTitlesInOrder)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                for (int i = 0; i < windowTitlesInOrder.Count; i++)
                {
                    SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE ClientGroupMembers SET DisplayOrder = $displayOrder WHERE GroupId = $groupId AND WindowTitle = $windowTitle";
                    command.Parameters.AddWithValue("$groupId", groupId);
                    command.Parameters.AddWithValue("$windowTitle", windowTitlesInOrder[i].Trim());
                    command.Parameters.AddWithValue("$displayOrder", i);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Gets all window titles that are not in any group for a specific profile.
        /// </summary>
        public List<string> GetUngroupedClients(long profileId)
        {
            List<string> ungrouped = new List<string>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get all window titles from ThumbnailSettings for this profile
            SqliteCommand allClientsCommand = connection.CreateCommand();
            allClientsCommand.CommandText = "SELECT DISTINCT WindowTitle FROM ThumbnailSettings WHERE ProfileId = $profileId";
            allClientsCommand.Parameters.AddWithValue("$profileId", profileId);

            HashSet<string> allClients = new HashSet<string>();
            using (SqliteDataReader reader = allClientsCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    allClients.Add(reader.GetString(0));
                }
            }

            // Get all window titles that are in groups
            SqliteCommand groupedCommand = connection.CreateCommand();
            groupedCommand.CommandText = @"
                SELECT DISTINCT cgm.WindowTitle
                FROM ClientGroupMembers cgm
                INNER JOIN ClientGroups cg ON cgm.GroupId = cg.Id
                WHERE cg.ProfileId = $profileId";
            groupedCommand.Parameters.AddWithValue("$profileId", profileId);

            HashSet<string> grouped = new HashSet<string>();
            using (SqliteDataReader reader = groupedCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    grouped.Add(reader.GetString(0));
                }
            }

            // Return clients that are not in any group
            ungrouped = allClients.Except(grouped).ToList();
            return ungrouped;
        }

        /// <summary>
        /// Updates the hotkeys for a client group.
        /// </summary>
        public void UpdateClientGroupHotkeys(long groupId, string forwardHotkey, string backwardHotkey)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE ClientGroups 
                SET CycleForwardHotkey = $forwardHotkey, CycleBackwardHotkey = $backwardHotkey
                WHERE Id = $groupId";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$forwardHotkey", forwardHotkey ?? string.Empty);
            command.Parameters.AddWithValue("$backwardHotkey", backwardHotkey ?? string.Empty);
            command.ExecuteNonQuery();
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
                    // If a window title appears in multiple groups, keep the first occurrence
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

            // Get all active window titles from thumbnail service
            // We'll need to pass this in or get it another way
            // For now, return the first client from the first group
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

