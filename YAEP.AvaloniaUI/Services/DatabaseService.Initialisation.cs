using ThumbnailConstants = YAEP.ThumbnailConstants;

namespace YAEP.Services
{
    /// <summary>
    /// Database initialization methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Initializes the database and creates necessary tables if they don't exist.
        /// </summary>
        private void InitializeDatabase()
        {
            WithConnection(connection =>
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS Profile (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        DeletedAt TEXT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 0
                    )");

                TryAddColumn(connection, "Profile", "DeletedAt TEXT NULL");
                TryAddColumn(connection, "Profile", "IsActive INTEGER NOT NULL DEFAULT 0");
                TryAddColumn(connection, "Profile", "SwitchHotkey TEXT NOT NULL DEFAULT ''");

                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ProcessesToPreview (
                        ProfileId INTEGER NOT NULL,
                        ProcessName TEXT NOT NULL,
                        PRIMARY KEY (ProfileId, ProcessName),
                        FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                    )");

                ExecuteNonQuery(connection, $@"
                    CREATE TABLE IF NOT EXISTS ThumbnailDefaultConfig (
                        ProfileId INTEGER NOT NULL PRIMARY KEY,
                        Width INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultThumbnailWidth},
                        Height INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultThumbnailHeight},
                        X INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultThumbnailX},
                        Y INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultThumbnailY},
                        Opacity REAL NOT NULL DEFAULT {ThumbnailConstants.DefaultThumbnailOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                        FocusBorderColor TEXT NOT NULL DEFAULT '{ThumbnailConstants.DefaultFocusBorderColor}',
                        FocusBorderThickness INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultFocusBorderThickness},
                        ShowTitleOverlay INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                    )");

                TryAddColumn(connection, "ThumbnailDefaultConfig", $"FocusBorderColor TEXT NOT NULL DEFAULT '{ThumbnailConstants.DefaultFocusBorderColor}'");
                TryAddColumn(connection, "ThumbnailDefaultConfig", "FocusBorderThickness INTEGER NOT NULL DEFAULT " + ThumbnailConstants.DefaultFocusBorderThickness);
                TryAddColumn(connection, "ThumbnailDefaultConfig", "ShowTitleOverlay INTEGER NOT NULL DEFAULT 1");

                ExecuteNonQuery(connection, $@"
                    CREATE TABLE IF NOT EXISTS ThumbnailSettings (
                        ProfileId INTEGER NOT NULL,
                        WindowTitle TEXT NOT NULL,
                        Width INTEGER NOT NULL,
                        Height INTEGER NOT NULL,
                        X INTEGER NOT NULL,
                        Y INTEGER NOT NULL,
                        Opacity REAL NOT NULL,
                        FocusBorderColor TEXT NOT NULL DEFAULT '{ThumbnailConstants.DefaultFocusBorderColor}',
                        FocusBorderThickness INTEGER NOT NULL DEFAULT {ThumbnailConstants.DefaultFocusBorderThickness},
                        ShowTitleOverlay INTEGER NOT NULL DEFAULT 1,
                        PRIMARY KEY (ProfileId, WindowTitle),
                        FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE
                    )");

                TryAddColumn(connection, "ThumbnailSettings", $"FocusBorderColor TEXT NOT NULL DEFAULT '{ThumbnailConstants.DefaultFocusBorderColor}'");
                TryAddColumn(connection, "ThumbnailSettings", "FocusBorderThickness INTEGER NOT NULL DEFAULT " + ThumbnailConstants.DefaultFocusBorderThickness);
                TryAddColumn(connection, "ThumbnailSettings", "ShowTitleOverlay INTEGER NOT NULL DEFAULT 1");

                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Key TEXT NOT NULL PRIMARY KEY,
                        Value TEXT NOT NULL
                    )");

                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS MumbleLinks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Url TEXT NOT NULL,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        IsSelected INTEGER NOT NULL DEFAULT 0
                    )");

                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS MumbleLinksOverlaySettings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        AlwaysOnTop INTEGER NOT NULL DEFAULT 1,
                        X INTEGER NOT NULL DEFAULT 100,
                        Y INTEGER NOT NULL DEFAULT 100,
                        Width INTEGER NOT NULL DEFAULT 300,
                        Height INTEGER NOT NULL DEFAULT 400
                    )");
            });

            InitializeClientGroupsTables();

            long profileCount = (long)(ExecuteScalar("SELECT COUNT(*) FROM Profile WHERE DeletedAt IS NULL") ?? 0L);
            if (profileCount == 0)
            {
                ExecuteNonQuery(@"
                    INSERT INTO Profile (Name)
                    VALUES ('Default')");

                long defaultProfileId = (long)(ExecuteScalar("SELECT Id FROM Profile WHERE Name = 'Default'") ?? 0L);

                SetCurrentProfile(defaultProfileId);
                SetThumbnailDefaultConfig(defaultProfileId, DefaultThumbnailSetting);
                AddProcessName(defaultProfileId, "exefile");
                CreateClientGroup(defaultProfileId, "Default");
            }
        }

        /// <summary>
        /// Initializes the client groups tables in the database.
        /// </summary>
        private void InitializeClientGroupsTables()
        {
            WithConnection(connection =>
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ClientGroups (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProfileId INTEGER NOT NULL,
                        Name TEXT NOT NULL,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        CycleForwardHotkey TEXT NOT NULL DEFAULT '',
                        CycleBackwardHotkey TEXT NOT NULL DEFAULT '',
                        FOREIGN KEY (ProfileId) REFERENCES Profile(Id) ON DELETE CASCADE,
                        UNIQUE(ProfileId, Name)
                    )");

                TryAddColumn(connection, "ClientGroups", "CycleForwardHotkey TEXT NOT NULL DEFAULT ''");
                TryAddColumn(connection, "ClientGroups", "CycleBackwardHotkey TEXT NOT NULL DEFAULT ''");

                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ClientGroupMembers (
                        GroupId INTEGER NOT NULL,
                        WindowTitle TEXT NOT NULL,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        PRIMARY KEY (GroupId, WindowTitle),
                        FOREIGN KEY (GroupId) REFERENCES ClientGroups(Id) ON DELETE CASCADE
                    )");
            });
        }
    }
}

