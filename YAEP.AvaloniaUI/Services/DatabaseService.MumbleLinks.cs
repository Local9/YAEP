using Microsoft.Data.Sqlite;
using YAEP.Helpers;
using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Mumble links management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {

        /// <summary>
        /// Creates a MumbleLink object from a SqliteDataReader.
        /// </summary>
        /// <param name="reader">The data reader positioned at the MumbleLink row.</param>
        /// <returns>A new MumbleLink object.</returns>
        private static MumbleLink MumbleLinkFromReader(SqliteDataReader reader)
        {
            return new MumbleLink
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Url = reader.GetString(2),
                DisplayOrder = reader.GetInt32(3),
                IsSelected = reader.GetInt64(4) != 0,
                ServerGroupId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                Hotkey = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                ServerGroupName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
            };
        }

        private const string MumbleLinksSelectColumns = "l.Id, l.Name, l.Url, l.DisplayOrder, l.IsSelected, l.ServerGroupId, l.Hotkey, g.Name AS ServerGroupName";
        private const string MumbleLinksFromClause = "MumbleLinks l LEFT JOIN MumbleServerGroups g ON l.ServerGroupId = g.Id";

        /// <summary>
        /// Gets all Mumble links ordered by DisplayOrder.
        /// </summary>
        /// <returns>List of Mumble links.</returns>
        public List<MumbleLink> GetMumbleLinks()
        {
            return GetMumbleLinks(null);
        }

        /// <summary>
        /// Gets Mumble links, optionally filtered by server group. When serverGroupId is null, returns all links.
        /// When serverGroupId is set, returns links that are in that group (via MumbleLinkGroups); a link can be in many groups.
        /// </summary>
        /// <param name="serverGroupId">Optional server group ID to filter by; null for all.</param>
        /// <returns>List of Mumble links ordered by DisplayOrder.</returns>
        public List<MumbleLink> GetMumbleLinks(long? serverGroupId)
        {
            List<MumbleLink> links = new List<MumbleLink>();
            if (serverGroupId == null)
            {
                ExecuteReader($"SELECT {MumbleLinksSelectColumns} FROM {MumbleLinksFromClause} ORDER BY l.DisplayOrder, l.Id",
                    reader => links.Add(MumbleLinkFromReader(reader)));
            }
            else
            {
                const string linkGroupFrom = "MumbleLinks l INNER JOIN MumbleLinkGroups lg ON l.Id = lg.LinkId LEFT JOIN MumbleServerGroups g ON lg.GroupId = g.Id";
                ExecuteReader($"SELECT l.Id, l.Name, l.Url, l.DisplayOrder, l.IsSelected, l.ServerGroupId, l.Hotkey, g.Name AS ServerGroupName FROM {linkGroupFrom} WHERE lg.GroupId = $gid ORDER BY l.DisplayOrder, l.Id",
                    reader => links.Add(MumbleLinkFromReader(reader)),
                    cmd => cmd.Parameters.AddWithValue("$gid", serverGroupId.Value));
            }
            return links;
        }

        /// <summary>
        /// Gets a Mumble link by ID.
        /// </summary>
        /// <param name="id">The link ID.</param>
        /// <returns>The Mumble link if found, null otherwise.</returns>
        public MumbleLink? GetMumbleLink(long id)
        {
            MumbleLink? link = null;
            ExecuteReader($"SELECT {MumbleLinksSelectColumns} FROM {MumbleLinksFromClause} WHERE l.Id = $id",
                reader =>
                {
                    if (link == null)
                        link = MumbleLinkFromReader(reader);
                },
                cmd => cmd.Parameters.AddWithValue("$id", id));
            return link;
        }

        /// <summary>
        /// Creates a new Mumble link. Extracts the name from the URL if not provided.
        /// </summary>
        /// <param name="url">The Mumble protocol URL.</param>
        /// <param name="name">Optional name. If not provided, extracted from URL.</param>
        /// <param name="serverGroupId">Optional server group to assign the link to.</param>
        /// <returns>The created link with its ID, or null if creation failed.</returns>
        public MumbleLink? CreateMumbleLink(string url, string? name = null, long? serverGroupId = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!SecurityValidationHelper.IsValidMumbleUrl(url))
            {
                throw new ArgumentException("Invalid Mumble URL format. URL must use mumble:// protocol and have a valid host.", nameof(url));
            }

            if (string.IsNullOrWhiteSpace(name))
                name = ExtractNameFromUrl(url);

            int nextOrder = Convert.ToInt32(ExecuteScalar("SELECT COALESCE(MAX(DisplayOrder), -1) FROM MumbleLinks") ?? -1) + 1;

            try
            {
                ExecuteNonQuery(@"
                    INSERT INTO MumbleLinks (Name, Url, DisplayOrder, IsSelected, ServerGroupId, Hotkey)
                    VALUES ($name, $url, $displayOrder, 0, NULL, '')",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$name", name.Trim());
                        cmd.Parameters.AddWithValue("$url", url.Trim());
                        cmd.Parameters.AddWithValue("$displayOrder", nextOrder);
                    });

                long newId = Convert.ToInt64(ExecuteScalar("SELECT last_insert_rowid()") ?? 0L);
                if (serverGroupId != null && newId != 0)
                {
                    ExecuteNonQuery(@"
                        INSERT OR IGNORE INTO MumbleLinkGroups (LinkId, GroupId) VALUES ($linkId, $groupId)",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$linkId", newId);
                            cmd.Parameters.AddWithValue("$groupId", serverGroupId.Value);
                        });
                    SyncLinkDisplayGroupId(newId);
                }

                MumbleLink? link = null;
                ExecuteReader($"SELECT {MumbleLinksSelectColumns} FROM {MumbleLinksFromClause} WHERE l.Id = $id",
                    reader =>
                    {
                        if (link == null)
                            link = MumbleLinkFromReader(reader);
                    },
                    cmd => cmd.Parameters.AddWithValue("$id", newId));
                return link;
            }
            catch (SqliteException)
            {
                return null;
            }
        }

        /// <summary>
        /// Updates a Mumble link's name, URL and hotkey. Group membership is updated separately via SetLinkGroups.
        /// </summary>
        /// <param name="id">The link ID.</param>
        /// <param name="name">The new name.</param>
        /// <param name="url">The new URL.</param>
        /// <param name="hotkey">Optional hotkey string; null or empty to clear.</param>
        public void UpdateMumbleLink(long id, string name, string url, string? hotkey = null)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                return;

            if (!SecurityValidationHelper.IsValidMumbleUrl(url))
            {
                throw new ArgumentException("Invalid Mumble URL format. URL must use mumble:// protocol and have a valid host.", nameof(url));
            }

            ExecuteNonQuery(@"
                UPDATE MumbleLinks
                SET Name = $name, Url = $url, Hotkey = $hotkey
                WHERE Id = $id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$name", name.Trim());
                    cmd.Parameters.AddWithValue("$url", url.Trim());
                    cmd.Parameters.AddWithValue("$hotkey", string.IsNullOrEmpty(hotkey) ? string.Empty : hotkey.Trim());
                });
        }

        /// <summary>
        /// Adds the given links to the given group (many-to-many). No-op if serverGroupId is null.
        /// </summary>
        public void UpdateMumbleLinksServerGroup(IEnumerable<long> ids, long? serverGroupId)
        {
            if (serverGroupId == null)
                return;
            List<long> idList = ids?.ToList() ?? new List<long>();
            if (idList.Count == 0)
                return;
            AddLinksToGroup(idList, serverGroupId.Value);
        }

        /// <summary>
        /// Gets the group IDs that a link belongs to (many-to-many).
        /// </summary>
        public List<long> GetLinkGroupIds(long linkId)
        {
            List<long> ids = new List<long>();
            ExecuteReader("SELECT GroupId FROM MumbleLinkGroups WHERE LinkId = $linkId ORDER BY GroupId",
                reader => ids.Add(reader.GetInt64(0)),
                cmd => cmd.Parameters.AddWithValue("$linkId", linkId));
            return ids;
        }

        /// <summary>
        /// Sets which groups a link belongs to (replaces existing memberships). Updates the legacy ServerGroupId for display.
        /// </summary>
        public void SetLinkGroups(long linkId, IReadOnlyList<long>? groupIds)
        {
            ExecuteNonQuery("DELETE FROM MumbleLinkGroups WHERE LinkId = $linkId",
                cmd => cmd.Parameters.AddWithValue("$linkId", linkId));

            if (groupIds != null && groupIds.Count > 0)
            {
                foreach (long groupId in groupIds)
                {
                    ExecuteNonQuery(@"
                        INSERT OR IGNORE INTO MumbleLinkGroups (LinkId, GroupId) VALUES ($linkId, $groupId)",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$linkId", linkId);
                            cmd.Parameters.AddWithValue("$groupId", groupId);
                        });
                }
            }

            SyncLinkDisplayGroupId(linkId);
        }

        /// <summary>
        /// Adds an existing link to a group (many-to-many). No-op if already in group. Syncs display ServerGroupId.
        /// </summary>
        public void AddLinkToGroup(long linkId, long groupId)
        {
            ExecuteNonQuery(@"
                INSERT OR IGNORE INTO MumbleLinkGroups (LinkId, GroupId) VALUES ($linkId, $groupId)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$linkId", linkId);
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                });
            SyncLinkDisplayGroupId(linkId);
        }

        /// <summary>
        /// Removes a link from a group (many-to-many). Syncs display ServerGroupId.
        /// </summary>
        public void RemoveLinkFromGroup(long linkId, long groupId)
        {
            ExecuteNonQuery("DELETE FROM MumbleLinkGroups WHERE LinkId = $linkId AND GroupId = $groupId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$linkId", linkId);
                    cmd.Parameters.AddWithValue("$groupId", groupId);
                });
            SyncLinkDisplayGroupId(linkId);
        }

        /// <summary>
        /// Adds multiple links to a group (many-to-many). Syncs each link's display ServerGroupId.
        /// </summary>
        public void AddLinksToGroup(IEnumerable<long> linkIds, long groupId)
        {
            List<long> idList = linkIds?.ToList() ?? new List<long>();
            foreach (long linkId in idList)
                AddLinkToGroup(linkId, groupId);
        }

        /// <summary>
        /// Sets MumbleLinks.ServerGroupId to the first group from MumbleLinkGroups for the link (for display), or null if none.
        /// </summary>
        private void SyncLinkDisplayGroupId(long linkId)
        {
            object? firstGroupId = ExecuteScalar("SELECT GroupId FROM MumbleLinkGroups WHERE LinkId = $linkId ORDER BY GroupId LIMIT 1",
                cmd => cmd.Parameters.AddWithValue("$linkId", linkId));
            ExecuteNonQuery("UPDATE MumbleLinks SET ServerGroupId = $serverGroupId WHERE Id = $id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$id", linkId);
                    cmd.Parameters.AddWithValue("$serverGroupId", firstGroupId ?? (object)DBNull.Value);
                });
        }

        /// <summary>
        /// Gets links that are not in the given group and match the search text (for add-to-group picker).
        /// </summary>
        /// <param name="groupId">Group to exclude links that are already in it.</param>
        /// <param name="searchText">Optional filter on Name or Url (case-insensitive contains); empty returns all not in group.</param>
        public List<MumbleLink> GetMumbleLinksForPicker(long groupId, string? searchText = null)
        {
            List<MumbleLink> links = new List<MumbleLink>();
            string search = (searchText ?? string.Empty).Trim();
            string pattern = string.IsNullOrEmpty(search) ? "%" : "%" + search.Replace("%", "\\%").Replace("_", "\\_") + "%";

            if (string.IsNullOrEmpty(search))
            {
                ExecuteReader(@"
                    SELECT l.Id, l.Name, l.Url, l.DisplayOrder, l.IsSelected, l.ServerGroupId, l.Hotkey, '' AS ServerGroupName
                    FROM MumbleLinks l
                    WHERE l.Id NOT IN (SELECT LinkId FROM MumbleLinkGroups WHERE GroupId = $gid)
                    ORDER BY l.DisplayOrder, l.Id",
                    reader => links.Add(MumbleLinkFromReader(reader)),
                    cmd => cmd.Parameters.AddWithValue("$gid", groupId));
            }
            else
            {
                ExecuteReader(@"
                    SELECT l.Id, l.Name, l.Url, l.DisplayOrder, l.IsSelected, l.ServerGroupId, l.Hotkey, '' AS ServerGroupName
                    FROM MumbleLinks l
                    WHERE l.Id NOT IN (SELECT LinkId FROM MumbleLinkGroups WHERE GroupId = $gid)
                      AND (l.Name LIKE $pattern ESCAPE '\' OR l.Url LIKE $pattern ESCAPE '\')
                    ORDER BY l.DisplayOrder, l.Id",
                    reader => links.Add(MumbleLinkFromReader(reader)),
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$gid", groupId);
                        cmd.Parameters.AddWithValue("$pattern", pattern);
                    });
            }
            return links;
        }

        /// <summary>
        /// Updates the hotkey for a Mumble link.
        /// </summary>
        public void UpdateMumbleLinkHotkey(long id, string? hotkey)
        {
            ExecuteNonQuery(@"
                UPDATE MumbleLinks
                SET Hotkey = $hotkey
                WHERE Id = $id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$hotkey", string.IsNullOrEmpty(hotkey) ? string.Empty : hotkey.Trim());
                });
        }

        /// <summary>
        /// Deletes a Mumble link.
        /// </summary>
        /// <param name="id">The link ID.</param>
        public void DeleteMumbleLink(long id)
        {
            object? orderResult = ExecuteScalar("SELECT DisplayOrder FROM MumbleLinks WHERE Id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id));

            ExecuteNonQuery("DELETE FROM MumbleLinks WHERE Id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id));

            if (orderResult != null)
            {
                int deletedOrder = Convert.ToInt32(orderResult);
                ExecuteNonQuery(@"
                    UPDATE MumbleLinks
                    SET DisplayOrder = DisplayOrder - 1
                    WHERE DisplayOrder > $deletedOrder",
                    cmd => cmd.Parameters.AddWithValue("$deletedOrder", deletedOrder));
            }
        }

        /// <summary>
        /// Updates the display order of Mumble links.
        /// </summary>
        /// <param name="linkIds">List of link IDs in the desired order.</param>
        public void UpdateMumbleLinksOrder(List<long> linkIds)
        {
            if (linkIds == null || linkIds.Count == 0)
                return;

            WithConnection(connection =>
            {
                for (int i = 0; i < linkIds.Count; i++)
                {
                    ExecuteNonQuery(connection, @"
                        UPDATE MumbleLinks
                        SET DisplayOrder = $order
                        WHERE Id = $id",
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue("$id", linkIds[i]);
                            cmd.Parameters.AddWithValue("$order", i);
                        });
                }
            });
        }

        /// <summary>
        /// Updates the selection state of a Mumble link.
        /// </summary>
        /// <param name="id">The link ID.</param>
        /// <param name="isSelected">Whether the link is selected for display.</param>
        public void UpdateMumbleLinkSelection(long id, bool isSelected)
        {
            ExecuteNonQuery(@"
                UPDATE MumbleLinks
                SET IsSelected = $isSelected
                WHERE Id = $id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$isSelected", isSelected ? 1 : 0);
                });
        }

        /// <summary>
        /// Gets all selected Mumble links ordered by DisplayOrder.
        /// </summary>
        /// <returns>List of selected Mumble links.</returns>
        public List<MumbleLink> GetSelectedMumbleLinks()
        {
            List<MumbleLink> links = new List<MumbleLink>();

            ExecuteReader($"SELECT {MumbleLinksSelectColumns} FROM {MumbleLinksFromClause} WHERE l.IsSelected = 1 ORDER BY l.DisplayOrder, l.Id",
                reader => links.Add(MumbleLinkFromReader(reader)));

            return links;
        }

        /// <summary>
        /// Extracts the name from a Mumble URL (between the last '/' and '?').
        /// </summary>
        /// <param name="url">The Mumble protocol URL.</param>
        /// <returns>The extracted name, or "Mumble Link" if extraction fails.</returns>
        private static string ExtractNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "Mumble Link";

            try
            {
                string urlWithoutProtocol = url;
                if (url.StartsWith("mumble://", StringComparison.OrdinalIgnoreCase))
                {
                    urlWithoutProtocol = url.Substring(9);
                }

                int lastSlashIndex = urlWithoutProtocol.LastIndexOf('/');
                int questionMarkIndex = urlWithoutProtocol.IndexOf('?');

                if (lastSlashIndex >= 0)
                {
                    int startIndex = lastSlashIndex + 1;
                    int length = questionMarkIndex > startIndex
                        ? questionMarkIndex - startIndex
                        : urlWithoutProtocol.Length - startIndex;

                    if (length > 0)
                    {
                        string name = urlWithoutProtocol.Substring(startIndex, length);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return Uri.UnescapeDataString(name);
                        }
                    }
                }

                if (questionMarkIndex > 0)
                {
                    string beforeQuery = urlWithoutProtocol.Substring(0, questionMarkIndex);
                    int lastSlash = beforeQuery.LastIndexOf('/');
                    if (lastSlash >= 0 && lastSlash < beforeQuery.Length - 1)
                    {
                        string name = beforeQuery.Substring(lastSlash + 1);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return Uri.UnescapeDataString(name);
                        }
                    }
                }
            }
            catch
            {
            }

            return "Mumble Link";
        }


        /// <summary>
        /// Gets the overlay window settings, creating default if none exist.
        /// </summary>
        /// <returns>The overlay settings.</returns>
        public MumbleLinksOverlaySettings GetMumbleLinksOverlaySettings()
        {
            MumbleLinksOverlaySettings? settings = null;
            ExecuteReader("SELECT AlwaysOnTop, X, Y, Width, Height FROM MumbleLinksOverlaySettings LIMIT 1",
                reader =>
                {
                    if (settings == null)
                    {
                        settings = new MumbleLinksOverlaySettings
                        {
                            AlwaysOnTop = reader.GetInt64(0) != 0,
                            X = reader.GetInt32(1),
                            Y = reader.GetInt32(2),
                            Width = reader.GetInt32(3),
                            Height = reader.GetInt32(4)
                        };
                    }
                });

            if (settings != null)
                return settings;

            MumbleLinksOverlaySettings defaultSettings = new MumbleLinksOverlaySettings();
            SaveMumbleLinksOverlaySettings(defaultSettings);
            return defaultSettings;
        }

        /// <summary>
        /// Saves the overlay window settings.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        public void SaveMumbleLinksOverlaySettings(MumbleLinksOverlaySettings settings)
        {
            long count = (long)(ExecuteScalar("SELECT COUNT(*) FROM MumbleLinksOverlaySettings") ?? 0L);

            if (count == 0)
            {
                ExecuteNonQuery(@"
                    INSERT INTO MumbleLinksOverlaySettings (AlwaysOnTop, X, Y, Width, Height)
                    VALUES ($alwaysOnTop, $x, $y, $width, $height)",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$alwaysOnTop", settings.AlwaysOnTop ? 1 : 0);
                        cmd.Parameters.AddWithValue("$x", settings.X);
                        cmd.Parameters.AddWithValue("$y", settings.Y);
                        cmd.Parameters.AddWithValue("$width", settings.Width);
                        cmd.Parameters.AddWithValue("$height", settings.Height);
                    });
            }
            else
            {
                ExecuteNonQuery(@"
                    UPDATE MumbleLinksOverlaySettings
                    SET AlwaysOnTop = $alwaysOnTop, X = $x, Y = $y, Width = $width, Height = $height
                    WHERE Id = (SELECT Id FROM MumbleLinksOverlaySettings LIMIT 1)",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$alwaysOnTop", settings.AlwaysOnTop ? 1 : 0);
                        cmd.Parameters.AddWithValue("$x", settings.X);
                        cmd.Parameters.AddWithValue("$y", settings.Y);
                        cmd.Parameters.AddWithValue("$width", settings.Width);
                        cmd.Parameters.AddWithValue("$height", settings.Height);
                    });
            }
        }
    }
}

