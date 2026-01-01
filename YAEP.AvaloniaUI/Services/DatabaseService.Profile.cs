using Microsoft.Data.Sqlite;

namespace YAEP.Services
{
    /// <summary>
    /// Profile management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Represents a profile in the database.
        /// </summary>
        public class Profile
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime? DeletedAt { get; set; }
            public bool IsActive { get; set; }
            public string SwitchHotkey { get; set; } = string.Empty;

            public bool IsDeleted => DeletedAt.HasValue;
        }

        /// <summary>
        /// Safely parses a DateTime string from the database.
        /// </summary>
        private static DateTime? ParseDateTime(string? dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
                return null;

            if (DateTime.TryParse(dateTimeString, out DateTime result))
                return result;

            return null;
        }

        /// <summary>
        /// Gets the active profile from the database.
        /// </summary>
        /// <returns>The active profile, or null if no profile is active.</returns>
        public Profile? GetActiveProfile()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE IsActive = 1 AND DeletedAt IS NULL LIMIT 1";

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Profile
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                    IsActive = reader.GetInt64(3) != 0,
                    SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                };
            }

            return null;
        }

        /// <summary>
        /// Gets the default profile (first profile, or profile named "Default" if it exists).
        /// </summary>
        /// <returns>The default profile, or null if no profiles exist.</returns>
        public Profile? GetDefaultProfile()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Try to get profile named "Default" first (not deleted)
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Name = 'Default' AND DeletedAt IS NULL LIMIT 1";

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Profile
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                    IsActive = reader.GetInt64(3) != 0,
                    SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                };
            }

            // If no "Default" profile, get the first non-deleted profile
            command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NULL ORDER BY Id LIMIT 1";

            using SqliteDataReader reader2 = command.ExecuteReader();
            if (reader2.Read())
            {
                return new Profile
                {
                    Id = reader2.GetInt64(0),
                    Name = reader2.GetString(1),
                    DeletedAt = reader2.IsDBNull(2) ? null : ParseDateTime(reader2.GetString(2)),
                    IsActive = reader2.GetInt64(3) != 0,
                    SwitchHotkey = reader2.IsDBNull(4) ? string.Empty : reader2.GetString(4)
                };
            }

            return null;
        }

        /// <summary>
        /// Checks if a profile is the default profile (lowest ID).
        /// </summary>
        /// <param name="profileId">The profile ID to check.</param>
        /// <returns>True if the profile is the default profile, false otherwise.</returns>
        public bool IsDefaultProfile(long profileId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT MIN(Id) FROM Profile WHERE DeletedAt IS NULL";
            object? minId = command.ExecuteScalar();
            return minId != null && Convert.ToInt64(minId) == profileId;
        }

        /// <summary>
        /// Gets all non-deleted profiles.
        /// </summary>
        /// <returns>List of profiles.</returns>
        public List<Profile> GetProfiles()
        {
            List<Profile> profiles = new List<Profile>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NULL ORDER BY Name";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                profiles.Add(new Profile
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                    IsActive = reader.GetInt64(3) != 0,
                    SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                });
            }

            return profiles;
        }

        /// <summary>
        /// Gets all deleted profiles.
        /// </summary>
        /// <returns>List of deleted profiles.</returns>
        public List<Profile> GetDeletedProfiles()
        {
            List<Profile> profiles = new List<Profile>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NOT NULL ORDER BY DeletedAt DESC";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                profiles.Add(new Profile
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                    IsActive = reader.GetInt64(3) != 0,
                    SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                });
            }

            return profiles;
        }

        /// <summary>
        /// Gets a profile by ID (including deleted profiles).
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <returns>The profile if found, null otherwise.</returns>
        public Profile? GetProfile(long profileId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Id = $profileId";
            command.Parameters.AddWithValue("$profileId", profileId);

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Profile
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                    IsActive = reader.GetInt64(3) != 0,
                    SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                };
            }

            return null;
        }

        /// <summary>
        /// Creates a new profile.
        /// </summary>
        /// <param name="name">The profile name.</param>
        /// <returns>The created profile with its ID, or null if creation failed.</returns>
        public Profile? CreateProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Check if a non-deleted profile with this name already exists
            SqliteCommand checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Profile WHERE Name = $name AND DeletedAt IS NULL";
            checkCommand.Parameters.AddWithValue("$name", name.Trim());
            long existingCount = (long)checkCommand.ExecuteScalar()!;

            if (existingCount > 0)
            {
                // Profile name already exists (non-deleted)
                return null;
            }

            // Check if a deleted profile with this name exists - if so, restore it instead
            SqliteCommand checkDeletedCommand = connection.CreateCommand();
            checkDeletedCommand.CommandText = "SELECT Id FROM Profile WHERE Name = $name AND DeletedAt IS NOT NULL LIMIT 1";
            checkDeletedCommand.Parameters.AddWithValue("$name", name.Trim());

            object? deletedProfileId = checkDeletedCommand.ExecuteScalar();
            if (deletedProfileId != null)
            {
                // Restore the deleted profile
                long profileId = Convert.ToInt64(deletedProfileId);
                RestoreProfile(profileId);

                // Get the restored profile
                return GetProfile(profileId);
            }

            // Create new profile
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Profile (Name, SwitchHotkey)
                VALUES ($name, '')";
            command.Parameters.AddWithValue("$name", name.Trim());

            try
            {
                command.ExecuteNonQuery();

                // Get the created profile ID using last_insert_rowid()
                SqliteCommand getProfileCommand = connection.CreateCommand();
                getProfileCommand.CommandText = "SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Id = last_insert_rowid()";

                using SqliteDataReader reader = getProfileCommand.ExecuteReader();
                if (reader.Read())
                {
                    long profileId = reader.GetInt64(0);

                    // Create default config for the new profile
                    SqliteCommand insertDefaultConfigCommand = connection.CreateCommand();
                    insertDefaultConfigCommand.CommandText = @"
                        INSERT INTO ThumbnailDefaultConfig (ProfileId, Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay)
                        VALUES ($profileId, 400, 300, 100, 100, 0.75, '#0078D4', 3, 1)";
                    insertDefaultConfigCommand.Parameters.AddWithValue("$profileId", profileId);
                    insertDefaultConfigCommand.ExecuteNonQuery();

                    // Add "exefile" as default process for the new profile
                    AddProcessName(profileId, "exefile");

                    return new Profile
                    {
                        Id = profileId,
                        Name = reader.GetString(1),
                        DeletedAt = reader.IsDBNull(2) ? null : ParseDateTime(reader.GetString(2)),
                        IsActive = reader.GetInt64(3) != 0,
                        SwitchHotkey = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    };
                }
            }
            catch (SqliteException)
            {
                // Profile name already exists or other error
                return null;
            }

            return null;
        }

        /// <summary>
        /// Updates a profile's name.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="name">The new profile name.</param>
        public void UpdateProfile(long profileId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Profile
                SET Name = $name
                WHERE Id = $profileId";
            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$name", name.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Soft deletes a profile by setting its DeletedAt timestamp.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        public void DeleteProfile(long profileId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand checkDefaultCommand = connection.CreateCommand();
            checkDefaultCommand.CommandText = "SELECT MIN(Id) FROM Profile WHERE DeletedAt IS NULL";
            object? minId = checkDefaultCommand.ExecuteScalar();
            if (minId != null && Convert.ToInt64(minId) == profileId)
            {
                return;
            }

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Profile
                SET DeletedAt = $deletedAt
                WHERE Id = $profileId AND DeletedAt IS NULL";
            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$deletedAt", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Restores a soft-deleted profile by clearing its DeletedAt timestamp.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        public void RestoreProfile(long profileId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Profile
                SET DeletedAt = NULL
                WHERE Id = $profileId AND DeletedAt IS NOT NULL";
            command.Parameters.AddWithValue("$profileId", profileId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Updates the hotkey for a profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="hotkey">The hotkey string.</param>
        public void UpdateProfileHotkey(long profileId, string hotkey)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Profile 
                SET SwitchHotkey = $hotkey
                WHERE Id = $profileId";
            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$hotkey", hotkey ?? string.Empty);
            command.ExecuteNonQuery();
        }
    }
}

