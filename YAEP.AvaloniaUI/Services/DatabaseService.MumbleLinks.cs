using Microsoft.Data.Sqlite;
using YAEP.Helpers;

namespace YAEP.Services
{
    /// <summary>
    /// Mumble links management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Represents a Mumble link in the database.
        /// </summary>
        public class MumbleLink
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public int DisplayOrder { get; set; }
            public bool IsSelected { get; set; }

            public void OpenLink()
            {
                if (string.IsNullOrWhiteSpace(Url) || !SecurityValidationHelper.IsValidMumbleUrl(Url))
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid or unsafe URL format: {Url}");
                    return;
                }

                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Url,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to open Mumble link: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets all Mumble links ordered by DisplayOrder.
        /// </summary>
        /// <returns>List of Mumble links.</returns>
        public List<MumbleLink> GetMumbleLinks()
        {
            List<MumbleLink> links = new List<MumbleLink>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Url, DisplayOrder, IsSelected FROM MumbleLinks ORDER BY DisplayOrder, Id";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                links.Add(new MumbleLink
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Url = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                    IsSelected = reader.GetInt64(4) != 0
                });
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
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Url, DisplayOrder, IsSelected FROM MumbleLinks WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new MumbleLink
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Url = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                    IsSelected = reader.GetInt64(4) != 0
                };
            }

            return null;
        }

        /// <summary>
        /// Creates a new Mumble link. Extracts the name from the URL if not provided.
        /// </summary>
        /// <param name="url">The Mumble protocol URL.</param>
        /// <param name="name">Optional name. If not provided, extracted from URL.</param>
        /// <returns>The created link with its ID, or null if creation failed.</returns>
        public MumbleLink? CreateMumbleLink(string url, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!SecurityValidationHelper.IsValidMumbleUrl(url))
            {
                throw new ArgumentException("Invalid Mumble URL format. URL must use mumble:// protocol and have a valid host.", nameof(url));
            }

            // Extract name from URL if not provided
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ExtractNameFromUrl(url);
            }

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get the maximum display order
            SqliteCommand maxOrderCommand = connection.CreateCommand();
            maxOrderCommand.CommandText = "SELECT COALESCE(MAX(DisplayOrder), -1) FROM MumbleLinks";
            object? maxOrderResult = maxOrderCommand.ExecuteScalar();
            int nextOrder = maxOrderResult != null ? Convert.ToInt32(maxOrderResult) + 1 : 0;

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MumbleLinks (Name, Url, DisplayOrder, IsSelected)
                VALUES ($name, $url, $displayOrder, 0)";
            command.Parameters.AddWithValue("$name", name.Trim());
            command.Parameters.AddWithValue("$url", url.Trim());
            command.Parameters.AddWithValue("$displayOrder", nextOrder);

            try
            {
                command.ExecuteNonQuery();

                // Get the created link ID
                SqliteCommand getLinkCommand = connection.CreateCommand();
                getLinkCommand.CommandText = "SELECT Id, Name, Url, DisplayOrder, IsSelected FROM MumbleLinks WHERE Id = last_insert_rowid()";

                using SqliteDataReader reader = getLinkCommand.ExecuteReader();
                if (reader.Read())
                {
                    return new MumbleLink
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Url = reader.GetString(2),
                        DisplayOrder = reader.GetInt32(3),
                        IsSelected = reader.GetInt64(4) != 0
                    };
                }
            }
            catch (SqliteException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Updates a Mumble link.
        /// </summary>
        /// <param name="id">The link ID.</param>
        /// <param name="name">The new name.</param>
        /// <param name="url">The new URL.</param>
        public void UpdateMumbleLink(long id, string name, string url)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                return;

            if (!SecurityValidationHelper.IsValidMumbleUrl(url))
            {
                throw new ArgumentException("Invalid Mumble URL format. URL must use mumble:// protocol and have a valid host.", nameof(url));
            }

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE MumbleLinks
                SET Name = $name, Url = $url
                WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$name", name.Trim());
            command.Parameters.AddWithValue("$url", url.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Deletes a Mumble link.
        /// </summary>
        /// <param name="id">The link ID.</param>
        public void DeleteMumbleLink(long id)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get the display order of the link being deleted
            SqliteCommand getOrderCommand = connection.CreateCommand();
            getOrderCommand.CommandText = "SELECT DisplayOrder FROM MumbleLinks WHERE Id = $id";
            getOrderCommand.Parameters.AddWithValue("$id", id);
            object? orderResult = getOrderCommand.ExecuteScalar();

            SqliteCommand deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM MumbleLinks WHERE Id = $id";
            deleteCommand.Parameters.AddWithValue("$id", id);
            deleteCommand.ExecuteNonQuery();

            // Reorder remaining links if needed
            if (orderResult != null)
            {
                int deletedOrder = Convert.ToInt32(orderResult);
                SqliteCommand reorderCommand = connection.CreateCommand();
                reorderCommand.CommandText = @"
                    UPDATE MumbleLinks
                    SET DisplayOrder = DisplayOrder - 1
                    WHERE DisplayOrder > $deletedOrder";
                reorderCommand.Parameters.AddWithValue("$deletedOrder", deletedOrder);
                reorderCommand.ExecuteNonQuery();
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

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            for (int i = 0; i < linkIds.Count; i++)
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE MumbleLinks
                    SET DisplayOrder = $order
                    WHERE Id = $id";
                command.Parameters.AddWithValue("$id", linkIds[i]);
                command.Parameters.AddWithValue("$order", i);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates the selection state of a Mumble link.
        /// </summary>
        /// <param name="id">The link ID.</param>
        /// <param name="isSelected">Whether the link is selected for display.</param>
        public void UpdateMumbleLinkSelection(long id, bool isSelected)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE MumbleLinks
                SET IsSelected = $isSelected
                WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$isSelected", isSelected ? 1 : 0);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Gets all selected Mumble links ordered by DisplayOrder.
        /// </summary>
        /// <returns>List of selected Mumble links.</returns>
        public List<MumbleLink> GetSelectedMumbleLinks()
        {
            List<MumbleLink> links = new List<MumbleLink>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Url, DisplayOrder, IsSelected FROM MumbleLinks WHERE IsSelected = 1 ORDER BY DisplayOrder, Id";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                links.Add(new MumbleLink
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Url = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                    IsSelected = reader.GetInt64(4) != 0
                });
            }

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
                // Remove the mumble:// protocol prefix if present
                string urlWithoutProtocol = url;
                if (url.StartsWith("mumble://", StringComparison.OrdinalIgnoreCase))
                {
                    urlWithoutProtocol = url.Substring(9);
                }

                // Find the last '/' before the '?'
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

                // Fallback: try to extract from the host part
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
                // If extraction fails, return default name
            }

            return "Mumble Link";
        }

        /// <summary>
        /// Represents overlay window settings for Mumble links.
        /// </summary>
        public class MumbleLinksOverlaySettings
        {
            public bool AlwaysOnTop { get; set; } = true;
            public int X { get; set; } = 100;
            public int Y { get; set; } = 100;
            public int Width { get; set; } = 300;
            public int Height { get; set; } = 400;
        }

        /// <summary>
        /// Gets the overlay window settings, creating default if none exist.
        /// </summary>
        /// <returns>The overlay settings.</returns>
        public MumbleLinksOverlaySettings GetMumbleLinksOverlaySettings()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT AlwaysOnTop, X, Y, Width, Height FROM MumbleLinksOverlaySettings LIMIT 1";

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new MumbleLinksOverlaySettings
                {
                    AlwaysOnTop = reader.GetInt64(0) != 0,
                    X = reader.GetInt32(1),
                    Y = reader.GetInt32(2),
                    Width = reader.GetInt32(3),
                    Height = reader.GetInt32(4)
                };
            }

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
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM MumbleLinksOverlaySettings";
            long count = (long)checkCommand.ExecuteScalar()!;

            if (count == 0)
            {
                SqliteCommand insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO MumbleLinksOverlaySettings (AlwaysOnTop, X, Y, Width, Height)
                    VALUES ($alwaysOnTop, $x, $y, $width, $height)";
                insertCommand.Parameters.AddWithValue("$alwaysOnTop", settings.AlwaysOnTop ? 1 : 0);
                insertCommand.Parameters.AddWithValue("$x", settings.X);
                insertCommand.Parameters.AddWithValue("$y", settings.Y);
                insertCommand.Parameters.AddWithValue("$width", settings.Width);
                insertCommand.Parameters.AddWithValue("$height", settings.Height);
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                SqliteCommand updateCommand = connection.CreateCommand();
                updateCommand.CommandText = @"
                    UPDATE MumbleLinksOverlaySettings
                    SET AlwaysOnTop = $alwaysOnTop, X = $x, Y = $y, Width = $width, Height = $height
                    WHERE Id = (SELECT Id FROM MumbleLinksOverlaySettings LIMIT 1)";
                updateCommand.Parameters.AddWithValue("$alwaysOnTop", settings.AlwaysOnTop ? 1 : 0);
                updateCommand.Parameters.AddWithValue("$x", settings.X);
                updateCommand.Parameters.AddWithValue("$y", settings.Y);
                updateCommand.Parameters.AddWithValue("$width", settings.Width);
                updateCommand.Parameters.AddWithValue("$height", settings.Height);
                updateCommand.ExecuteNonQuery();
            }
        }
    }
}

