using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Drawer settings methods for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
        /// <summary>
        /// Gets the drawer settings from the database.
        /// </summary>
        /// <returns>The drawer settings, or default values if not found.</returns>
        public DrawerSettings GetDrawerSettings()
        {
            DrawerSettings defaultSettings = new DrawerSettings
            {
                ScreenIndex = 0,
                Side = DrawerSide.Right,
                Width = 400,
                Height = 600,
                IsVisible = false,
                IsEnabled = false
            };

            try
            {
                object? screenIndexObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerScreenIndex"));

                object? sideObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerSide"));

                object? widthObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerWidth"));

                object? heightObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerHeight"));

                object? isVisibleObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerIsVisible"));

                object? isEnabledObj = ExecuteScalar("SELECT Value FROM AppSettings WHERE Key = $key",
                    cmd => cmd.Parameters.AddWithValue("$key", "DrawerIsEnabled"));

                if (screenIndexObj != null && screenIndexObj != DBNull.Value && int.TryParse(screenIndexObj.ToString(), out int screenIndex))
                {
                    defaultSettings.ScreenIndex = screenIndex;
                }

                if (sideObj != null && sideObj != DBNull.Value && Enum.TryParse<DrawerSide>(sideObj.ToString(), out DrawerSide side))
                {
                    defaultSettings.Side = side;
                }

                if (widthObj != null && widthObj != DBNull.Value && int.TryParse(widthObj.ToString(), out int width))
                {
                    defaultSettings.Width = width;
                }

                if (heightObj != null && heightObj != DBNull.Value && int.TryParse(heightObj.ToString(), out int height))
                {
                    defaultSettings.Height = height;
                }

                if (isVisibleObj != null && isVisibleObj != DBNull.Value && bool.TryParse(isVisibleObj.ToString(), out bool isVisible))
                {
                    defaultSettings.IsVisible = isVisible;
                }

                if (isEnabledObj != null && isEnabledObj != DBNull.Value && bool.TryParse(isEnabledObj.ToString(), out bool isEnabled))
                {
                    defaultSettings.IsEnabled = isEnabled;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting drawer settings: {ex.Message}");
            }

            return defaultSettings;
        }

        /// <summary>
        /// Saves the drawer settings to the database.
        /// </summary>
        /// <param name="settings">The drawer settings to save.</param>
        public void SaveDrawerSettings(DrawerSettings settings)
        {
            if (settings == null)
                return;

            try
            {
                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerScreenIndex");
                        cmd.Parameters.AddWithValue("$value", settings.ScreenIndex.ToString());
                    });

                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerSide");
                        cmd.Parameters.AddWithValue("$value", settings.Side.ToString());
                    });

                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerWidth");
                        cmd.Parameters.AddWithValue("$value", settings.Width.ToString());
                    });

                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerHeight");
                        cmd.Parameters.AddWithValue("$value", settings.Height.ToString());
                    });

                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerIsVisible");
                        cmd.Parameters.AddWithValue("$value", settings.IsVisible.ToString());
                    });

                ExecuteNonQuery(@"
                    INSERT INTO AppSettings (Key, Value)
                    VALUES ($key, $value)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = $value",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("$key", "DrawerIsEnabled");
                        cmd.Parameters.AddWithValue("$value", settings.IsEnabled.ToString());
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving drawer settings: {ex.Message}");
            }
        }
    }
}
