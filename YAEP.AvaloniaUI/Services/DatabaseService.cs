using Microsoft.Data.Sqlite;
using System.IO;

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

            _databasePath = Path.Combine(baseDirectory, "settings.db");
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

        /// <summary>
        /// Initializes the database and creates necessary tables if they don't exist.
        /// </summary>
        private void InitializeDatabase()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            SqliteCommand createProfileTableCommand = connection.CreateCommand();
            createProfileTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Profile (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    DeletedAt TEXT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 0
                )";
            createProfileTableCommand.ExecuteNonQuery();

            try
            {
                SqliteCommand addDeletedAtColumnCommand = connection.CreateCommand();
                addDeletedAtColumnCommand.CommandText = "ALTER TABLE Profile ADD COLUMN DeletedAt TEXT NULL";
                addDeletedAtColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            try
            {
                SqliteCommand addIsActiveColumnCommand = connection.CreateCommand();
                addIsActiveColumnCommand.CommandText = "ALTER TABLE Profile ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 0";
                addIsActiveColumnCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            SqliteCommand createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ProcessesToPreview (
                    ProfileId INTEGER NOT NULL,
                    ProcessName TEXT NOT NULL,
                    PRIMARY KEY (ProfileId, ProcessName),
                    FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                )";

            createTableCommand.ExecuteNonQuery();

            SqliteCommand createDefaultConfigCommand = connection.CreateCommand();
            createDefaultConfigCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ThumbnailDefaultConfig (
                    ProfileId INTEGER NOT NULL PRIMARY KEY,
                    Width INTEGER NOT NULL DEFAULT 400,
                    Height INTEGER NOT NULL DEFAULT 300,
                    X INTEGER NOT NULL DEFAULT 100,
                    Y INTEGER NOT NULL DEFAULT 100,
                    Opacity REAL NOT NULL DEFAULT 0.75,
                    FocusBorderColor TEXT NOT NULL DEFAULT '#0078D4',
                    FocusBorderThickness INTEGER NOT NULL DEFAULT 3,
                    ShowTitleOverlay INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                )";
            createDefaultConfigCommand.ExecuteNonQuery();

            try
            {
                SqliteCommand addFocusBorderColorCommand = connection.CreateCommand();
                addFocusBorderColorCommand.CommandText = "ALTER TABLE ThumbnailDefaultConfig ADD COLUMN FocusBorderColor TEXT NOT NULL DEFAULT '#0078D4'";
                addFocusBorderColorCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            try
            {
                SqliteCommand addFocusBorderThicknessCommand = connection.CreateCommand();
                addFocusBorderThicknessCommand.CommandText = "ALTER TABLE ThumbnailDefaultConfig ADD COLUMN FocusBorderThickness INTEGER NOT NULL DEFAULT 3";
                addFocusBorderThicknessCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            try
            {
                SqliteCommand addShowTitleOverlayCommand = connection.CreateCommand();
                addShowTitleOverlayCommand.CommandText = "ALTER TABLE ThumbnailDefaultConfig ADD COLUMN ShowTitleOverlay INTEGER NOT NULL DEFAULT 1";
                addShowTitleOverlayCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            SqliteCommand createThumbnailSettingsCommand = connection.CreateCommand();
            createThumbnailSettingsCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ThumbnailSettings (
                    ProfileId INTEGER NOT NULL,
                    WindowTitle TEXT NOT NULL,
                    Width INTEGER NOT NULL,
                    Height INTEGER NOT NULL,
                    X INTEGER NOT NULL,
                    Y INTEGER NOT NULL,
                    Opacity REAL NOT NULL,
                    FocusBorderColor TEXT NOT NULL DEFAULT '#0078D4',
                    FocusBorderThickness INTEGER NOT NULL DEFAULT 3,
                    ShowTitleOverlay INTEGER NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProfileId, WindowTitle),
                    FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                )";
            createThumbnailSettingsCommand.ExecuteNonQuery();

            try
            {
                SqliteCommand addFocusBorderColorCommand = connection.CreateCommand();
                addFocusBorderColorCommand.CommandText = "ALTER TABLE ThumbnailSettings ADD COLUMN FocusBorderColor TEXT NOT NULL DEFAULT '#0078D4'";
                addFocusBorderColorCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            try
            {
                SqliteCommand addFocusBorderThicknessCommand = connection.CreateCommand();
                addFocusBorderThicknessCommand.CommandText = "ALTER TABLE ThumbnailSettings ADD COLUMN FocusBorderThickness INTEGER NOT NULL DEFAULT 3";
                addFocusBorderThicknessCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            try
            {
                SqliteCommand addShowTitleOverlayCommand = connection.CreateCommand();
                addShowTitleOverlayCommand.CommandText = "ALTER TABLE ThumbnailSettings ADD COLUMN ShowTitleOverlay INTEGER NOT NULL DEFAULT 1";
                addShowTitleOverlayCommand.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
            }

            SqliteCommand createAppSettingsCommand = connection.CreateCommand();
            createAppSettingsCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS AppSettings (
                    Key TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL
                )";
            createAppSettingsCommand.ExecuteNonQuery();

            SqliteCommand createMumbleLinksCommand = connection.CreateCommand();
            createMumbleLinksCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS MumbleLinks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Url TEXT NOT NULL,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    IsSelected INTEGER NOT NULL DEFAULT 0
                )";
            createMumbleLinksCommand.ExecuteNonQuery();

            SqliteCommand createMumbleLinksOverlaySettingsCommand = connection.CreateCommand();
            createMumbleLinksOverlaySettingsCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS MumbleLinksOverlaySettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AlwaysOnTop INTEGER NOT NULL DEFAULT 1,
                    X INTEGER NOT NULL DEFAULT 100,
                    Y INTEGER NOT NULL DEFAULT 100,
                    Width INTEGER NOT NULL DEFAULT 300,
                    Height INTEGER NOT NULL DEFAULT 400
                )";
            createMumbleLinksOverlaySettingsCommand.ExecuteNonQuery();

            connection.Close();

            InitializeClientGroupsTables();

            connection.Open();

            SqliteCommand checkProfileCommand = connection.CreateCommand();
            checkProfileCommand.CommandText = "SELECT COUNT(*) FROM Profile WHERE DeletedAt IS NULL";
            long profileCount = (long)checkProfileCommand.ExecuteScalar()!;
            if (profileCount == 0)
            {
                SqliteCommand insertDefaultProfileCommand = connection.CreateCommand();
                insertDefaultProfileCommand.CommandText = @"
                    INSERT INTO Profile (Name)
                    VALUES ('Default')";
                insertDefaultProfileCommand.ExecuteNonQuery();

                SqliteCommand getProfileIdCommand = connection.CreateCommand();
                getProfileIdCommand.CommandText = "SELECT Id FROM Profile WHERE Name = 'Default'";
                long defaultProfileId = (long)getProfileIdCommand.ExecuteScalar()!;

                SqliteCommand setActiveCommand = connection.CreateCommand();
                setActiveCommand.CommandText = "UPDATE Profile SET IsActive = 1 WHERE Id = $profileId";
                setActiveCommand.Parameters.AddWithValue("$profileId", defaultProfileId);
                setActiveCommand.ExecuteNonQuery();

                SqliteCommand insertDefaultConfigCommand = connection.CreateCommand();
                insertDefaultConfigCommand.CommandText = @"
                    INSERT INTO ThumbnailDefaultConfig (ProfileId, Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay)
                    VALUES ($profileId, 400, 300, 100, 100, 0.75, '#0078D4', 3, 1)";
                insertDefaultConfigCommand.Parameters.AddWithValue("$profileId", defaultProfileId);
                insertDefaultConfigCommand.ExecuteNonQuery();

                SqliteCommand insertDefaultProcessCommand = connection.CreateCommand();
                insertDefaultProcessCommand.CommandText = @"
                    INSERT INTO ProcessesToPreview (ProfileId, ProcessName)
                    VALUES ($profileId, $processName)";
                insertDefaultProcessCommand.Parameters.AddWithValue("$profileId", defaultProfileId);
                insertDefaultProcessCommand.Parameters.AddWithValue("$processName", "exefile");
                insertDefaultProcessCommand.ExecuteNonQuery();
            }

            connection.Close();
        }

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

                using SqliteConnection connection = new SqliteConnection(_connectionString);
                connection.Open();

                SqliteCommand clearActiveCommand = connection.CreateCommand();
                clearActiveCommand.CommandText = "UPDATE Profile SET IsActive = 0";
                clearActiveCommand.ExecuteNonQuery();

                SqliteCommand setActiveCommand = connection.CreateCommand();
                setActiveCommand.CommandText = "UPDATE Profile SET IsActive = 1 WHERE Id = $profileId";
                setActiveCommand.Parameters.AddWithValue("$profileId", profileId);
                setActiveCommand.ExecuteNonQuery();

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
