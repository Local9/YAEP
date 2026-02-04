using Microsoft.Data.Sqlite;
using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Mumble server groups management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        private static MumbleServerGroup MumbleServerGroupFromReader(SqliteDataReader reader)
        {
            return new MumbleServerGroup
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                DisplayOrder = reader.GetInt32(2)
            };
        }

        /// <summary>
        /// Gets all Mumble server groups ordered by DisplayOrder.
        /// </summary>
        public List<MumbleServerGroup> GetMumbleServerGroups()
        {
            List<MumbleServerGroup> groups = new List<MumbleServerGroup>();
            ExecuteReader(
                "SELECT Id, Name, DisplayOrder FROM MumbleServerGroups ORDER BY DisplayOrder, Id",
                reader => groups.Add(MumbleServerGroupFromReader(reader)));
            return groups;
        }

        /// <summary>
        /// Gets a Mumble server group by ID.
        /// </summary>
        public MumbleServerGroup? GetMumbleServerGroup(long id)
        {
            MumbleServerGroup? group = null;
            ExecuteReader(
                "SELECT Id, Name, DisplayOrder FROM MumbleServerGroups WHERE Id = $id",
                reader =>
                {
                    if (group == null)
                        group = MumbleServerGroupFromReader(reader);
                },
                cmd => cmd.Parameters.AddWithValue("$id", id));
            return group;
        }

        /// <summary>
        /// Creates a new Mumble server group.
        /// </summary>
        public MumbleServerGroup? CreateMumbleServerGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            int nextOrder = Convert.ToInt32(ExecuteScalar("SELECT COALESCE(MAX(DisplayOrder), -1) FROM MumbleServerGroups") ?? -1) + 1;
            ExecuteNonQuery(@"
                INSERT INTO MumbleServerGroups (Name, DisplayOrder)
                VALUES ($name, $displayOrder)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$name", name.Trim());
                    cmd.Parameters.AddWithValue("$displayOrder", nextOrder);
                });

            MumbleServerGroup? group = null;
            ExecuteReader(
                "SELECT Id, Name, DisplayOrder FROM MumbleServerGroups WHERE Id = last_insert_rowid()",
                reader =>
                {
                    if (group == null)
                        group = MumbleServerGroupFromReader(reader);
                });
            return group;
        }

        /// <summary>
        /// Updates a Mumble server group's name.
        /// </summary>
        public void UpdateMumbleServerGroup(long id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            ExecuteNonQuery(@"
                UPDATE MumbleServerGroups
                SET Name = $name
                WHERE Id = $id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$name", name.Trim());
                });
        }

        /// <summary>
        /// Deletes a Mumble server group. Links in this group will have ServerGroupId set to NULL.
        /// </summary>
        public void DeleteMumbleServerGroup(long id)
        {
            ExecuteNonQuery("UPDATE MumbleLinks SET ServerGroupId = NULL WHERE ServerGroupId = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id));
            ExecuteNonQuery("DELETE FROM MumbleServerGroups WHERE Id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id));
        }

        /// <summary>
        /// Updates the display order of Mumble server groups.
        /// </summary>
        public void UpdateMumbleServerGroupsOrder(List<long> groupIds)
        {
            if (groupIds == null || groupIds.Count == 0)
                return;

            WithConnection(connection =>
            {
                for (int i = 0; i < groupIds.Count; i++)
                {
                    ExecuteNonQuery(connection, @"
                        UPDATE MumbleServerGroups
                        SET DisplayOrder = $order
                        WHERE Id = $id",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$id", groupIds[i]);
                            cmd.Parameters.AddWithValue("$order", i);
                        });
                }
            });
        }
    }
}
