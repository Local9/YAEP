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
        /// Creates a Profile object from a SqliteDataReader.
        /// </summary>
        /// <param name="reader">The data reader positioned at the profile row.</param>
        /// <returns>A new Profile object.</returns>
        private static Profile ProfileFromReader(SqliteDataReader reader)
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

        /// <summary>
        /// Gets the active profile from the database.
        /// </summary>
        /// <returns>The active profile, or null if no profile is active.</returns>
        public Profile? GetActiveProfile()
        {
            Profile? profile = null;
            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE IsActive = 1 AND DeletedAt IS NULL LIMIT 1",
                reader =>
                {
                    if (profile == null)
                    {
                        profile = ProfileFromReader(reader);
                    }
                });
            return profile;
        }

        /// <summary>
        /// Gets the default profile (first profile, or profile named "Default" if it exists).
        /// </summary>
        /// <returns>The default profile, or null if no profiles exist.</returns>
        public Profile? GetDefaultProfile()
        {
            Profile? profile = null;
            
            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Name = 'Default' AND DeletedAt IS NULL LIMIT 1",
                reader =>
                {
                    if (profile == null)
                    {
                        profile = ProfileFromReader(reader);
                    }
                });

            if (profile != null)
                return profile;

            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NULL ORDER BY Id LIMIT 1",
                reader =>
                {
                    if (profile == null)
                    {
                        profile = ProfileFromReader(reader);
                    }
                });

            return profile;
        }

        /// <summary>
        /// Checks if a profile is the default profile (lowest ID).
        /// </summary>
        /// <param name="profileId">The profile ID to check.</param>
        /// <returns>True if the profile is the default profile, false otherwise.</returns>
        public bool IsDefaultProfile(long profileId)
        {
            object? minId = ExecuteScalar("SELECT MIN(Id) FROM Profile WHERE DeletedAt IS NULL");
            return minId != null && Convert.ToInt64(minId) == profileId;
        }

        /// <summary>
        /// Gets all non-deleted profiles.
        /// </summary>
        /// <returns>List of profiles.</returns>
        public List<Profile> GetProfiles()
        {
            List<Profile> profiles = new List<Profile>();

            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NULL ORDER BY Name",
                reader => profiles.Add(ProfileFromReader(reader)));

            return profiles;
        }

        /// <summary>
        /// Gets all deleted profiles.
        /// </summary>
        /// <returns>List of deleted profiles.</returns>
        public List<Profile> GetDeletedProfiles()
        {
            List<Profile> profiles = new List<Profile>();

            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE DeletedAt IS NOT NULL ORDER BY DeletedAt DESC",
                reader => profiles.Add(ProfileFromReader(reader)));

            return profiles;
        }

        /// <summary>
        /// Gets a profile by ID (including deleted profiles).
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <returns>The profile if found, null otherwise.</returns>
        public Profile? GetProfile(long profileId)
        {
            Profile? profile = null;
            ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Id = $profileId",
                reader =>
                {
                    if (profile == null)
                    {
                        profile = ProfileFromReader(reader);
                    }
                },
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));
            return profile;
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

            string trimmedName = name.Trim();

            long existingCount = (long)(ExecuteScalar("SELECT COUNT(*) FROM Profile WHERE Name = $name AND DeletedAt IS NULL",
                cmd => cmd.Parameters.AddWithValue("$name", trimmedName)) ?? 0L);

            if (existingCount > 0)
            {
                return null;
            }

            object? deletedProfileId = ExecuteScalar("SELECT Id FROM Profile WHERE Name = $name AND DeletedAt IS NOT NULL LIMIT 1",
                cmd => cmd.Parameters.AddWithValue("$name", trimmedName));
            
            if (deletedProfileId != null)
            {
                long profileId = Convert.ToInt64(deletedProfileId);
                RestoreProfile(profileId);

                return GetProfile(profileId);
            }

            try
            {
                ExecuteNonQuery(@"
                    INSERT INTO Profile (Name, SwitchHotkey)
                    VALUES ($name, '')",
                    cmd => cmd.Parameters.AddWithValue("$name", trimmedName));

                Profile? profile = null;
                ExecuteReader("SELECT Id, Name, DeletedAt, IsActive, SwitchHotkey FROM Profile WHERE Id = last_insert_rowid()",
                    reader =>
                    {
                        if (profile == null)
                        {
                            long profileId = reader.GetInt64(0);
                            SetThumbnailDefaultConfig(profileId, DefaultThumbnailSetting);
                            AddProcessName(profileId, "exefile");
                            profile = GetProfile(profileId);
                        }
                    });

                return profile;
            }
            catch (SqliteException)
            {
                return null;
            }
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

            ExecuteNonQuery(@"
                UPDATE Profile
                SET Name = $name
                WHERE Id = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$name", name.Trim());
                });
        }

        /// <summary>
        /// Soft deletes a profile by setting its DeletedAt timestamp.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        public void DeleteProfile(long profileId)
        {
            object? minId = ExecuteScalar("SELECT MIN(Id) FROM Profile WHERE DeletedAt IS NULL");
            if (minId != null && Convert.ToInt64(minId) == profileId)
            {
                return;
            }

            ExecuteNonQuery(@"
                UPDATE Profile
                SET DeletedAt = $deletedAt
                WHERE Id = $profileId AND DeletedAt IS NULL",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$deletedAt", DateTime.UtcNow.ToString("O"));
                });
        }

        /// <summary>
        /// Restores a soft-deleted profile by clearing its DeletedAt timestamp.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        public void RestoreProfile(long profileId)
        {
            ExecuteNonQuery(@"
                UPDATE Profile
                SET DeletedAt = NULL
                WHERE Id = $profileId AND DeletedAt IS NOT NULL",
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));
        }

        /// <summary>
        /// Updates the hotkey for a profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="hotkey">The hotkey string.</param>
        public void UpdateProfileHotkey(long profileId, string hotkey)
        {
            ExecuteNonQuery(@"
                UPDATE Profile 
                SET SwitchHotkey = $hotkey
                WHERE Id = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$hotkey", hotkey ?? string.Empty);
                });
        }
    }
}

