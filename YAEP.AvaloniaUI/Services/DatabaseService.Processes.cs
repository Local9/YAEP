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

            ExecuteReader("SELECT ProcessName FROM ProcessesToPreview WHERE ProfileId = $profileId ORDER BY ProcessName",
                reader => processNames.Add(reader.GetString(0)),
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

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

            ExecuteNonQuery(@"
                INSERT OR IGNORE INTO ProcessesToPreview (ProfileId, ProcessName)
                VALUES ($profileId, $processName)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$processName", processName.Trim());
                });
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

            ExecuteNonQuery("DELETE FROM ProcessesToPreview WHERE ProfileId = $profileId AND ProcessName = $processName",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$processName", processName.Trim());
                });
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

            long count = (long)(ExecuteScalar("SELECT COUNT(*) FROM ProcessesToPreview WHERE ProfileId = $profileId AND ProcessName = $processName",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$processName", processName.Trim());
                }) ?? 0L);

            return count > 0;
        }
    }
}

