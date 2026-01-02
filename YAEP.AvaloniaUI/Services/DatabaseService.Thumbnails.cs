using Microsoft.Data.Sqlite;

namespace YAEP.Services
{
    /// <summary>
    /// Thumbnail configuration methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Represents thumbnail configuration settings.
        /// </summary>
        public class ThumbnailConfig
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public double Opacity { get; set; }
            public string FocusBorderColor { get; set; } = "#0078D4"; // Default to system accent blue
            public int FocusBorderThickness { get; set; } = 3;
            public bool ShowTitleOverlay { get; set; } = true; // Default to showing title overlay
        }

        /// <summary>
        /// Gets the default thumbnail configuration for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <returns>Default thumbnail configuration.</returns>
        public ThumbnailConfig GetThumbnailDefaultConfig(long profileId)
        {
            ThumbnailConfig? config = null;
            ExecuteReader("SELECT Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay FROM ThumbnailDefaultConfig WHERE ProfileId = $profileId",
                reader =>
                {
                    if (config == null)
                    {
                        config = new ThumbnailConfig
                        {
                            Width = reader.GetInt32(0),
                            Height = reader.GetInt32(1),
                            X = reader.GetInt32(2),
                            Y = reader.GetInt32(3),
                            Opacity = reader.GetDouble(4),
                            FocusBorderColor = reader.IsDBNull(5) ? "#0078D4" : reader.GetString(5),
                            FocusBorderThickness = reader.IsDBNull(6) ? 3 : reader.GetInt32(6),
                            ShowTitleOverlay = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetBoolean(7) : true
                        };
                        System.Diagnostics.Debug.WriteLine($"GetThumbnailDefaultConfig: Loaded for ProfileId {profileId} - Color={config.FocusBorderColor}, Thickness={config.FocusBorderThickness}");
                    }
                },
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

            if (config != null)
                return config;

            System.Diagnostics.Debug.WriteLine($"GetThumbnailDefaultConfig: No record found for ProfileId {profileId}, using hardcoded defaults");
            return new ThumbnailConfig
            {
                Width = 400,
                Height = 300,
                X = 100,
                Y = 100,
                Opacity = 0.75,
                FocusBorderColor = "#0078D4",
                FocusBorderThickness = 3,
                ShowTitleOverlay = true
            };
        }

        /// <summary>
        /// Updates the default thumbnail configuration for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="config">The configuration to save.</param>
        public void SetThumbnailDefaultConfig(long profileId, ThumbnailConfig config)
        {
            if (config == null)
                return;

            int rowsAffected = ExecuteNonQuery(@"
                INSERT INTO ThumbnailDefaultConfig (ProfileId, Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay)
                VALUES ($profileId, $width, $height, $x, $y, $opacity, $focusBorderColor, $focusBorderThickness, $showTitleOverlay)
                ON CONFLICT(ProfileId) DO UPDATE SET
                    Width = $width,
                    Height = $height,
                    X = $x,
                    Y = $y,
                    Opacity = $opacity,
                    FocusBorderColor = $focusBorderColor,
                    FocusBorderThickness = $focusBorderThickness,
                    ShowTitleOverlay = $showTitleOverlay",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$width", config.Width);
                    cmd.Parameters.AddWithValue("$height", config.Height);
                    cmd.Parameters.AddWithValue("$x", config.X);
                    cmd.Parameters.AddWithValue("$y", config.Y);
                    cmd.Parameters.AddWithValue("$opacity", config.Opacity);
                    cmd.Parameters.AddWithValue("$focusBorderColor", config.FocusBorderColor ?? "#0078D4");
                    cmd.Parameters.AddWithValue("$focusBorderThickness", config.FocusBorderThickness);
                    cmd.Parameters.AddWithValue("$showTitleOverlay", config.ShowTitleOverlay ? 1 : 0);
                });
            System.Diagnostics.Debug.WriteLine($"SetThumbnailDefaultConfig: Saved for ProfileId {profileId} - Color={config.FocusBorderColor}, Thickness={config.FocusBorderThickness}, Rows affected={rowsAffected}");
        }

        /// <summary>
        /// Gets thumbnail settings for a specific window title in a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="windowTitle">The window title to get settings for.</param>
        /// <returns>Thumbnail settings if found, null otherwise.</returns>
        public ThumbnailConfig? GetThumbnailSettings(long profileId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return null;

            ThumbnailConfig? config = null;
            ExecuteReader("SELECT Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay FROM ThumbnailSettings WHERE ProfileId = $profileId AND WindowTitle = $windowTitle",
                reader =>
                {
                    if (config == null)
                    {
                        config = new ThumbnailConfig
                        {
                            Width = reader.GetInt32(0),
                            Height = reader.GetInt32(1),
                            X = reader.GetInt32(2),
                            Y = reader.GetInt32(3),
                            Opacity = reader.GetDouble(4),
                            FocusBorderColor = reader.IsDBNull(5) ? "#0078D4" : reader.GetString(5),
                            FocusBorderThickness = reader.IsDBNull(6) ? 3 : reader.GetInt32(6),
                            ShowTitleOverlay = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetBoolean(7) : true
                        };
                    }
                },
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                });

            return config;
        }

        /// <summary>
        /// Gets thumbnail settings for a window title in a specific profile, or returns defaults if not found.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="windowTitle">The window title to get settings for.</param>
        /// <returns>Thumbnail settings (per-window if exists, otherwise defaults).</returns>
        public ThumbnailConfig GetThumbnailSettingsOrDefault(long profileId, string windowTitle)
        {
            ThumbnailConfig? settings = GetThumbnailSettings(profileId, windowTitle);
            return settings ?? GetThumbnailDefaultConfig(profileId);
        }

        /// <summary>
        /// Saves thumbnail settings for a specific window title in a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="windowTitle">The window title to save settings for.</param>
        /// <param name="config">The configuration to save.</param>
        public void SaveThumbnailSettings(long profileId, string windowTitle, ThumbnailConfig config)
        {
            if (string.IsNullOrWhiteSpace(windowTitle) || config == null)
            {
                System.Diagnostics.Debug.WriteLine($"SaveThumbnailSettings: Invalid parameters - windowTitle: '{windowTitle}', config: {(config == null ? "null" : "not null")}");
                return;
            }

            try
            {
                int rowsAffected = ExecuteNonQuery(@"
                    INSERT INTO ThumbnailSettings (ProfileId, WindowTitle, Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay)
                    VALUES ($profileId, $windowTitle, $width, $height, $x, $y, $opacity, $focusBorderColor, $focusBorderThickness, $showTitleOverlay)
                    ON CONFLICT(ProfileId, WindowTitle) DO UPDATE SET
                        Width = $width,
                        Height = $height,
                        X = $x,
                        Y = $y,
                        Opacity = $opacity,
                        FocusBorderColor = $focusBorderColor,
                        FocusBorderThickness = $focusBorderThickness,
                        ShowTitleOverlay = $showTitleOverlay",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$profileId", profileId);
                        cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                        cmd.Parameters.AddWithValue("$width", config.Width);
                        cmd.Parameters.AddWithValue("$height", config.Height);
                        cmd.Parameters.AddWithValue("$x", config.X);
                        cmd.Parameters.AddWithValue("$y", config.Y);
                        cmd.Parameters.AddWithValue("$opacity", config.Opacity);
                        cmd.Parameters.AddWithValue("$focusBorderColor", config.FocusBorderColor ?? "#0078D4");
                        cmd.Parameters.AddWithValue("$focusBorderThickness", config.FocusBorderThickness);
                        cmd.Parameters.AddWithValue("$showTitleOverlay", config.ShowTitleOverlay ? 1 : 0);
                    });

                System.Diagnostics.Debug.WriteLine($"SaveThumbnailSettings: Saved settings for '{windowTitle}' (ProfileId: {profileId}, Rows affected: {rowsAffected})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveThumbnailSettings: Error saving settings for '{windowTitle}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Represents a thumbnail setting entry with window title.
        /// </summary>
        public class ThumbnailSetting
        {
            public string WindowTitle { get; set; } = string.Empty;
            public ThumbnailConfig Config { get; set; } = new();
        }

        /// <summary>
        /// Gets all thumbnail settings for a specific profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <returns>List of thumbnail settings with window titles.</returns>
        public List<ThumbnailSetting> GetAllThumbnailSettings(long profileId)
        {
            List<ThumbnailSetting> settings = new List<ThumbnailSetting>();

            ExecuteReader("SELECT WindowTitle, Width, Height, X, Y, Opacity, FocusBorderColor, FocusBorderThickness, ShowTitleOverlay FROM ThumbnailSettings WHERE ProfileId = $profileId ORDER BY WindowTitle",
                reader => settings.Add(new ThumbnailSetting
                {
                    WindowTitle = reader.GetString(0),
                    Config = new ThumbnailConfig
                    {
                        Width = reader.GetInt32(1),
                        Height = reader.GetInt32(2),
                        X = reader.GetInt32(3),
                        Y = reader.GetInt32(4),
                        Opacity = reader.GetDouble(5),
                        FocusBorderColor = reader.IsDBNull(6) ? "#0078D4" : reader.GetString(6),
                        FocusBorderThickness = reader.IsDBNull(7) ? 3 : reader.GetInt32(7),
                        ShowTitleOverlay = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetBoolean(8) : true
                    }
                }),
                cmd => cmd.Parameters.AddWithValue("$profileId", profileId));

            return settings;
        }

        /// <summary>
        /// Updates all thumbnail settings for a profile with the default configuration.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        public void UpdateAllThumbnailSettingsWithDefault(long profileId)
        {
            ThumbnailConfig defaultConfig = GetThumbnailDefaultConfig(profileId);

            ExecuteNonQuery(@"
                UPDATE ThumbnailSettings
                SET Width = $width,
                    Height = $height,
                    Opacity = $opacity,
                    FocusBorderColor = $focusBorderColor,
                    FocusBorderThickness = $focusBorderThickness,
                    ShowTitleOverlay = $showTitleOverlay
                WHERE ProfileId = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$width", defaultConfig.Width);
                    cmd.Parameters.AddWithValue("$height", defaultConfig.Height);
                    cmd.Parameters.AddWithValue("$opacity", defaultConfig.Opacity);
                    cmd.Parameters.AddWithValue("$focusBorderColor", defaultConfig.FocusBorderColor ?? "#0078D4");
                    cmd.Parameters.AddWithValue("$focusBorderThickness", defaultConfig.FocusBorderThickness);
                    cmd.Parameters.AddWithValue("$showTitleOverlay", defaultConfig.ShowTitleOverlay ? 1 : 0);
                });
        }

        /// <summary>
        /// Updates border settings for all thumbnail settings in a profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="borderColor">The border color in hex format.</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        public void UpdateAllThumbnailBorderSettings(long profileId, string borderColor, int borderThickness)
        {
            int rowsAffected = ExecuteNonQuery(@"
                UPDATE ThumbnailSettings
                SET FocusBorderColor = $focusBorderColor,
                    FocusBorderThickness = $focusBorderThickness
                WHERE ProfileId = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$focusBorderColor", borderColor ?? "#0078D4");
                    cmd.Parameters.AddWithValue("$focusBorderThickness", borderThickness);
                });
            System.Diagnostics.Debug.WriteLine($"UpdateAllThumbnailBorderSettings: Updated {rowsAffected} thumbnail setting(s) for ProfileId {profileId} with color={borderColor}, thickness={borderThickness}");
        }

        /// <summary>
        /// Updates size and opacity for all thumbnail settings in a profile, preserving positions.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        public void UpdateAllThumbnailSizeAndOpacity(long profileId, int width, int height, double opacity)
        {
            int rowsAffected = ExecuteNonQuery(@"
                UPDATE ThumbnailSettings
                SET Width = $width,
                    Height = $height,
                    Opacity = $opacity
                WHERE ProfileId = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$width", width);
                    cmd.Parameters.AddWithValue("$height", height);
                    cmd.Parameters.AddWithValue("$opacity", opacity);
                });
            System.Diagnostics.Debug.WriteLine($"UpdateAllThumbnailSizeAndOpacity: Updated {rowsAffected} thumbnail setting(s) for ProfileId {profileId} with width={width}, height={height}, opacity={opacity}");
        }

        /// <summary>
        /// Updates title overlay visibility for all thumbnail settings in a profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="showTitleOverlay">Whether to show the title overlay.</param>
        public void UpdateAllThumbnailTitleOverlay(long profileId, bool showTitleOverlay)
        {
            int rowsAffected = ExecuteNonQuery(@"
                UPDATE ThumbnailSettings
                SET ShowTitleOverlay = $showTitleOverlay
                WHERE ProfileId = $profileId",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$showTitleOverlay", showTitleOverlay ? 1 : 0);
                });
            System.Diagnostics.Debug.WriteLine($"UpdateAllThumbnailTitleOverlay: Updated {rowsAffected} thumbnail setting(s) for ProfileId {profileId} with showTitleOverlay={showTitleOverlay}");
        }

        /// <summary>
        /// Deletes thumbnail settings for a specific window title in a profile.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="windowTitle">The window title to delete settings for.</param>
        public void DeleteThumbnailSettings(long profileId, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            ExecuteNonQuery("DELETE FROM ThumbnailSettings WHERE ProfileId = $profileId AND WindowTitle = $windowTitle",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$windowTitle", windowTitle.Trim());
                });
        }

        /// <summary>
        /// Deletes thumbnail settings for window titles that might match a process name.
        /// This attempts to find thumbnail settings where the window title contains the process name.
        /// </summary>
        /// <param name="profileId">The profile ID.</param>
        /// <param name="processName">The process name to match against window titles.</param>
        public void DeleteThumbnailSettingsByProcessName(long profileId, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            string normalizedProcessName = processName;
            if (normalizedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalizedProcessName = normalizedProcessName.Substring(0, normalizedProcessName.Length - 4);
            }

            ExecuteNonQuery(@"
                DELETE FROM ThumbnailSettings 
                WHERE ProfileId = $profileId 
                AND (WindowTitle LIKE $pattern1 OR WindowTitle LIKE $pattern2 OR WindowTitle = $processName OR WindowTitle = $processNameExe)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$profileId", profileId);
                    cmd.Parameters.AddWithValue("$pattern1", $"%{normalizedProcessName}%");
                    cmd.Parameters.AddWithValue("$pattern2", $"%{processName}%");
                    cmd.Parameters.AddWithValue("$processName", normalizedProcessName);
                    cmd.Parameters.AddWithValue("$processNameExe", processName);
                });
        }
    }
}

