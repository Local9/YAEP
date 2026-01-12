using Microsoft.Data.Sqlite;

namespace YAEP.Services
{
    /// <summary>
    /// Application settings methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Gets whether thumbnail dragging is enabled.
        /// </summary>
        /// <returns>True if dragging is enabled, false otherwise. Defaults to true.</returns>
        public bool GetThumbnailDraggingEnabled()
        {
            object? result = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                cmd => cmd.Parameters.AddWithValue("$key", "EnableThumbnailDragging"));

            if (result != null && result != DBNull.Value)
            {
                if (bool.TryParse(result.ToString(), out bool value))
                {
                    return value;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets whether thumbnail dragging is enabled.
        /// </summary>
        /// <param name="enabled">True to enable dragging, false to disable.</param>
        public void SetThumbnailDraggingEnabled(bool enabled)
        {
            ExecuteNonQuery(@"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$key", "EnableThumbnailDragging");
                    cmd.Parameters.AddWithValue("$value", enabled.ToString());
                });
        }

        /// <summary>
        /// Gets an application setting by key.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or null if not found.</returns>
        public string? GetAppSetting(string key)
        {
            object? result = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                cmd => cmd.Parameters.AddWithValue("$key", key));

            if (result != null && result != DBNull.Value)
            {
                return result.ToString();
            }

            return null;
        }

        /// <summary>
        /// Sets an application setting by key.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        public void SetAppSetting(string key, string value)
        {
            ExecuteNonQuery(@"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$key", key);
                    cmd.Parameters.AddWithValue("$value", value ?? string.Empty);
                });
        }

        /// <summary>
        /// Gets whether the application should start hidden.
        /// </summary>
        /// <returns>True if the application should start hidden, false otherwise. Defaults to false.</returns>
        public bool GetStartHidden()
        {
            object? result = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                cmd => cmd.Parameters.AddWithValue("$key", "StartHidden"));

            if (result != null && result != DBNull.Value)
            {
                if (bool.TryParse(result.ToString(), out bool value))
                {
                    return value;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets whether the application should start hidden.
        /// </summary>
        /// <param name="startHidden">True to start hidden, false otherwise.</param>
        public void SetStartHidden(bool startHidden)
        {
            ExecuteNonQuery(@"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$key", "StartHidden");
                    cmd.Parameters.AddWithValue("$value", startHidden.ToString());
                });
        }

        /// <summary>
        /// Gets the list of ignored keys for hotkey filtering.
        /// </summary>
        /// <returns>List of ignored key names (e.g., "F24", "F13"). Returns empty list if not set.</returns>
        public List<string> GetIgnoredKeys()
        {
            string? result = GetAppSetting("IgnoredKeys");
            
            if (string.IsNullOrWhiteSpace(result))
            {
                return new List<string>();
            }

            return result.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        /// <summary>
        /// Sets the list of ignored keys for hotkey filtering.
        /// </summary>
        /// <param name="keys">List of ignored key names (e.g., "F24", "F13").</param>
        public void SetIgnoredKeys(List<string> keys)
        {
            string value = keys != null && keys.Count > 0
                ? string.Join(",", keys)
                : string.Empty;

            SetAppSetting("IgnoredKeys", value);
        }
    }
}

