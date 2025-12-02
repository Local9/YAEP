using Microsoft.Data.Sqlite;

namespace YAEP.Services
{
    /// <summary>
    /// Process management methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Gets all process names from the ProcessesToPreview table for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <returns>List of process names.</returns>
        public List<string> GetProcessNames(long profileId)
        {
            List<string> processNames = new List<string>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT ProcessName FROM ProcessesToPreview WHERE ProfileId = $profileId ORDER BY ProcessName";
            command.Parameters.AddWithValue("$profileId", profileId);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                processNames.Add(reader.GetString(0));
            }

            return processNames;
        }

        /// <summary>
        /// Adds a process name to the ProcessesToPreview table for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="processName">The process name to add.</param>
        public void AddProcessName(long profileId, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO ProcessesToPreview (ProfileId, ProcessName)
                VALUES ($profileId, $processName)";

            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$processName", processName.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Removes a process name from the ProcessesToPreview table for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="processName">The process name to remove.</param>
        public void RemoveProcessName(long profileId, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ProcessesToPreview WHERE ProfileId = $profileId AND ProcessName = $processName";

            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$processName", processName.Trim());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Checks if a process name exists in the ProcessesToPreview table for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="processName">The process name to check.</param>
        /// <returns>True if the process name exists, false otherwise.</returns>
        public bool ProcessNameExists(long profileId, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ProcessesToPreview WHERE ProfileId = $profileId AND ProcessName = $processName";

            command.Parameters.AddWithValue("$profileId", profileId);
            command.Parameters.AddWithValue("$processName", processName.Trim());
            long count = (long)command.ExecuteScalar()!;

            return count > 0;
        }
    }
}

