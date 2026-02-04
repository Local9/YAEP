using Microsoft.Data.Sqlite;
using System.IO;
using YAEP.Helpers;
using YAEP.Models;
using ThumbnailConstants = YAEP.ThumbnailConstants;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing SQLite database operations.
    /// </summary>
    public partial class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _connectionString;
        private Profile? _currentProfile;

        /// <summary>
        /// Event raised when the current profile changes.
        /// </summary>
        public event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

        public ThumbnailConfig DefaultThumbnailSetting = new ThumbnailConfig { Width = ThumbnailConstants.DefaultThumbnailWidth, Height = ThumbnailConstants.DefaultThumbnailHeight, X = ThumbnailConstants.DefaultThumbnailX, Y = ThumbnailConstants.DefaultThumbnailY, Opacity = ThumbnailConstants.DefaultThumbnailOpacity, FocusBorderColor = ThumbnailConstants.DefaultFocusBorderColor, FocusBorderThickness = ThumbnailConstants.DefaultFocusBorderThickness, ShowTitleOverlay = true };

        public DatabaseService()
        {
            string? baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }

            if (string.IsNullOrEmpty(baseDirectory))
            {
                throw new InvalidOperationException("Unable to determine application base directory");
            }

            try
            {
                baseDirectory = SecurityValidationHelper.ValidateAndNormalizePath(baseDirectory);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Invalid base directory path: {ex.Message}", ex);
            }

            _databasePath = Path.Combine(baseDirectory, "settings.db");

            try
            {
                string fullDbPath = Path.GetFullPath(_databasePath);
                string fullBaseDir = Path.GetFullPath(baseDirectory);

                if (!fullDbPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Database path is outside the application base directory");
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
            {
                throw new InvalidOperationException($"Invalid database path: {ex.Message}", ex);
            }

            _connectionString = $"Data Source={_databasePath}";

            InitializeDatabase();

            _currentProfile = GetActiveProfile() ?? GetDefaultProfile();

            if (_currentProfile != null && GetActiveProfile() == null)
            {
                SetCurrentProfile(_currentProfile.Id);
            }
        }

        /// <summary>
        /// Gets or sets the current active profile.
        /// </summary>
        public Profile? CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (_currentProfile?.Id != value?.Id)
                {
                    Profile? oldProfile = _currentProfile;
                    _currentProfile = value;
                    OnProfileChanged(oldProfile, value);
                }
            }
        }

        /// <summary>
        /// Gets the connection string for the database.
        /// </summary>
        public string ConnectionString => _connectionString;

        /// <summary>
        /// Gets the database file path.
        /// </summary>
        public string DatabasePath => _databasePath;

        #region Database Connection Helpers

        /// <summary>
        /// Executes a command that doesn't return data.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        /// <returns>The number of rows affected.</returns>
        private int ExecuteNonQuery(string commandText, Action<SqliteCommand>? parameters = null)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            return ExecuteNonQuery(connection, commandText, parameters);
        }

        /// <summary>
        /// Executes a command that doesn't return data using an existing connection.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        /// <returns>The number of rows affected.</returns>
        private int ExecuteNonQuery(SqliteConnection connection, string commandText, Action<SqliteCommand>? parameters = null)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            parameters?.Invoke(command);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a command that returns a single scalar value.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        /// <returns>The scalar result, or null if no result.</returns>
        private object? ExecuteScalar(string commandText, Action<SqliteCommand>? parameters = null)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            return ExecuteScalar(connection, commandText, parameters);
        }

        /// <summary>
        /// Executes a command that returns a single scalar value using an existing connection.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        /// <returns>The scalar result, or null if no result.</returns>
        private object? ExecuteScalar(SqliteConnection connection, string commandText, Action<SqliteCommand>? parameters = null)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            parameters?.Invoke(command);
            return command.ExecuteScalar();
        }

        /// <summary>
        /// Executes a command that returns a data reader, processing each row with the provided action.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="processRow">Action to process each row from the reader.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        private void ExecuteReader(string commandText, Action<SqliteDataReader> processRow, Action<SqliteCommand>? parameters = null)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            ExecuteReader(connection, commandText, processRow, parameters);
        }

        /// <summary>
        /// Executes a command that returns a data reader, processing each row with the provided action using an existing connection.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="processRow">Action to process each row from the reader.</param>
        /// <param name="parameters">Optional action to add parameters to the command.</param>
        private void ExecuteReader(SqliteConnection connection, string commandText, Action<SqliteDataReader> processRow, Action<SqliteCommand>? parameters = null)
        {
            SqliteCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            parameters?.Invoke(command);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                processRow(reader);
            }
        }

        /// <summary>
        /// Executes a command within a transaction.
        /// </summary>
        /// <param name="action">Action to perform within the transaction.</param>
        private void ExecuteTransaction(Action<SqliteConnection> action)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                action(connection);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Attempts to add a column to a table, ignoring the error if the column already exists.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="columnDefinition">The column definition (e.g., "ColumnName TYPE NOT NULL DEFAULT 'value'").</param>
        private void TryAddColumn(SqliteConnection connection, string tableName, string columnDefinition)
        {
            try
            {
                ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnDefinition}");
            }
            catch (SqliteException)
            {
            }
        }

        /// <summary>
        /// Gets a connection for operations that need to keep the connection open (e.g., multiple commands in sequence).
        /// </summary>
        /// <param name="action">Action to perform with the connection.</param>
        private void WithConnection(Action<SqliteConnection> action)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            action(connection);
        }

        #endregion

        /// <summary>
        /// Sets the current profile by ID and persists it to the database.
        /// </summary>
        /// <param name="profileId">The profile ID to set as current.</param>
        public void SetCurrentProfile(long profileId)
        {
            Profile? profile = GetProfile(profileId);
            if (profile != null && !profile.IsDeleted)
            {
                Profile? oldProfile = _currentProfile;

                if (oldProfile != null && oldProfile.Id == profileId)
                {
                    return;
                }

                WithConnection(connection =>
                {
                    ExecuteNonQuery(connection, "UPDATE Profile SET IsActive = 0");
                    ExecuteNonQuery(connection, "UPDATE Profile SET IsActive = 1 WHERE Id = $profileId",
                        cmd => cmd.Parameters.AddWithValue("$profileId", profileId));
                });

                profile.IsActive = true;
                _currentProfile = profile;
                OnProfileChanged(oldProfile, profile);
            }
        }

        /// <summary>
        /// Raises the ProfileChanged event.
        /// </summary>
        /// <param name="oldProfile">The previous profile.</param>
        /// <param name="newProfile">The new profile.</param>
        protected virtual void OnProfileChanged(Profile? oldProfile, Profile? newProfile)
        {
            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(oldProfile, newProfile));
        }
    }
}
