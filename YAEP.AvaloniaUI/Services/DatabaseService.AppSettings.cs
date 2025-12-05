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
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key";
            command.Parameters.AddWithValue("$key", "EnableThumbnailDragging");

            object? result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                if (bool.TryParse(result.ToString(), out bool value))
                {
                    return value;
                }
            }

            // Default to true if not set
            return true;
        }

        /// <summary>
        /// Sets whether thumbnail dragging is enabled.
        /// </summary>
        /// <param name="enabled">True to enable dragging, false to disable.</param>
        public void SetThumbnailDraggingEnabled(bool enabled)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value";
            command.Parameters.AddWithValue("$key", "EnableThumbnailDragging");
            command.Parameters.AddWithValue("$value", enabled.ToString());
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Gets an application setting by key.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or null if not found.</returns>
        public string? GetAppSetting(string key)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key";
            command.Parameters.AddWithValue("$key", key);

            object? result = command.ExecuteScalar();
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
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value ?? string.Empty);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Gets whether the application should start hidden.
        /// </summary>
        /// <returns>True if the application should start hidden, false otherwise. Defaults to false.</returns>
        public bool GetStartHidden()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key";
            command.Parameters.AddWithValue("$key", "StartHidden");

            object? result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                if (bool.TryParse(result.ToString(), out bool value))
                {
                    return value;
                }
            }

            // Default to false if not set
            return false;
        }

        /// <summary>
        /// Sets whether the application should start hidden.
        /// </summary>
        /// <param name="startHidden">True to start hidden, false otherwise.</param>
        public void SetStartHidden(bool startHidden)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET
                    Value = $value";
            command.Parameters.AddWithValue("$key", "StartHidden");
            command.Parameters.AddWithValue("$value", startHidden.ToString());
            command.ExecuteNonQuery();
        }
    }
}

