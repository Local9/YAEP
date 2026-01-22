using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using YAEP.Helpers;
using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing EVE Online client profiles.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class EveOnlineProfileService
    {
        private const string LocalAppDataEvePath = @"CCP\EVE";
        private const string RegistryKeyPath32 = @"SOFTWARE\CCP\EVE";
        private const string RegistryKeyPath64 = @"SOFTWARE\WOW6432Node\CCP\EVE";
        private const string RegistryKeyPathUser = @"SOFTWARE\CCP\EVE";

        /// <summary>
        /// Finds the EVE Online installation path from Windows registry.
        /// </summary>
        /// <returns>The installation path, or null if not found.</returns>
        public string? FindEveInstallationPath()
        {
            string? path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath64, "exefile");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath32, "exefile");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.CurrentUser, RegistryKeyPathUser, "exefile");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath64, "InstallPath");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath32, "InstallPath");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath64, "ExePath");
            if (!string.IsNullOrEmpty(path))
                return path;

            path = TryGetRegistryValue(Registry.LocalMachine, RegistryKeyPath32, "ExePath");
            if (!string.IsNullOrEmpty(path))
                return path;

            return null;
        }

        private string? TryGetRegistryValue(RegistryKey baseKey, string subKeyPath, string valueName)
        {
            try
            {
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath);
                if (key != null)
                {
                    object? value = key.GetValue(valueName);
                    if (value is string strValue && !string.IsNullOrEmpty(strValue))
                    {
                        return strValue;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Converts an installation path to the profile folder naming convention.
        /// </summary>
        /// <param name="installPath">The full path to the EVE executable or installation directory.</param>
        /// <param name="serverName">The server identifier (e.g., "tranquility" for "tq").</param>
        /// <returns>The converted folder name.</returns>
        public string ConvertPathToProfileFolderName(string installPath, string serverName)
        {
            if (string.IsNullOrEmpty(installPath))
                throw new ArgumentException("Install path cannot be null or empty", nameof(installPath));

            string sharedCachePath = installPath;

            if (File.Exists(installPath))
            {
                sharedCachePath = Path.GetDirectoryName(installPath) ?? installPath;
            }

            string[] parts = sharedCachePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int sharedCacheIndex = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("SharedCache", StringComparison.OrdinalIgnoreCase))
                {
                    sharedCacheIndex = i;
                    break;
                }
            }

            if (sharedCacheIndex >= 0 && sharedCacheIndex < parts.Length - 1)
            {
                List<string> relevantParts = new List<string>();
                for (int i = 0; i <= sharedCacheIndex + 1; i++)
                {
                    if (i < parts.Length)
                        relevantParts.Add(parts[i]);
                }
                sharedCachePath = string.Join(Path.DirectorySeparatorChar.ToString(), relevantParts);
            }

            string converted = sharedCachePath.ToLowerInvariant()
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_')
                .Replace(' ', '_');

            converted = converted.TrimEnd('_');
            converted += "_" + serverName.ToLowerInvariant();

            return converted;
        }

        /// <summary>
        /// Gets the local app data EVE directory path.
        /// </summary>
        /// <returns>The path to %LOCALAPPDATA%\CCP\EVE\</returns>
        public string GetLocalAppDataEvePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, LocalAppDataEvePath);
        }

        /// <summary>
        /// Discovers all server folders in the EVE profiles directory.
        /// </summary>
        /// <returns>List of server folder names.</returns>
        public List<string> GetAllServers()
        {
            List<string> servers = new List<string>();
            string evePath = GetLocalAppDataEvePath();

            if (!Directory.Exists(evePath))
                return servers;

            try
            {
                string[] directories = Directory.GetDirectories(evePath);
                foreach (string dir in directories)
                {
                    string folderName = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        servers.Add(folderName);
                    }
                }
            }
            catch
            {
            }

            return servers;
        }

        /// <summary>
        /// Gets all profiles for a specific server folder.
        /// </summary>
        /// <param name="serverFolderName">The server folder name.</param>
        /// <returns>List of profiles found in the server folder.</returns>
        public List<EveOnlineProfile> GetServerProfiles(string serverFolderName)
        {
            List<EveOnlineProfile> profiles = new List<EveOnlineProfile>();
            string evePath = GetLocalAppDataEvePath();
            string serverPath = Path.Combine(evePath, serverFolderName);

            if (!Directory.Exists(serverPath))
                return profiles;

            try
            {
                string[] directories = Directory.GetDirectories(serverPath, "settings_*");
                foreach (string dir in directories)
                {
                    string folderName = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(folderName) && folderName.StartsWith("settings_", StringComparison.OrdinalIgnoreCase))
                    {
                        string profileName = folderName.Substring("settings_".Length);
                        bool isDefault = profileName.Equals("Default", StringComparison.OrdinalIgnoreCase);

                        profiles.Add(new EveOnlineProfile
                        {
                            ServerName = serverFolderName,
                            ProfileName = profileName,
                            FullPath = dir,
                            IsDefault = isDefault
                        });
                    }
                }
            }
            catch
            {
            }

            return profiles;
        }

        /// <summary>
        /// Discovers all characters in a profile folder.
        /// </summary>
        /// <param name="profilePath">The full path to the profile folder.</param>
        /// <returns>List of characters found in the profile.</returns>
        public List<EveOnlineCharacter> GetProfileCharacters(string profilePath)
        {
            List<EveOnlineCharacter> characters = new List<EveOnlineCharacter>();

            if (!Directory.Exists(profilePath))
                return characters;

            try
            {
                string[] files = Directory.GetFiles(profilePath, "core_char_*");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("core_char_", StringComparison.OrdinalIgnoreCase))
                    {
                        string? idPart = ExtractIdFromFileName(fileName, "core_char_");
                        if (idPart != null && IsValidNumericId(idPart))
                        {
                            FileInfo fileInfo = new FileInfo(file);
                            characters.Add(new EveOnlineCharacter
                            {
                                CharacterId = idPart,
                                CharacterName = idPart,
                                FilePath = file,
                                ProfilePath = profilePath,
                                FileModifiedDate = fileInfo.LastWriteTime
                            });
                        }
                    }
                }
            }
            catch
            {
            }

            return characters;
        }

        /// <summary>
        /// Discovers all users in a profile folder.
        /// </summary>
        /// <param name="profilePath">The full path to the profile folder.</param>
        /// <returns>List of users found in the profile.</returns>
        public List<EveOnlineUser> GetProfileUsers(string profilePath)
        {
            List<EveOnlineUser> users = new List<EveOnlineUser>();

            if (!Directory.Exists(profilePath))
                return users;

            try
            {
                string[] files = Directory.GetFiles(profilePath, "core_user_*");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("core_user_", StringComparison.OrdinalIgnoreCase))
                    {
                        string? idPart = ExtractIdFromFileName(fileName, "core_user_");
                        if (idPart != null && IsValidNumericId(idPart))
                        {
                            FileInfo fileInfo = new FileInfo(file);
                            users.Add(new EveOnlineUser
                            {
                                UserId = idPart,
                                FilePath = file,
                                ProfilePath = profilePath,
                                FileModifiedDate = fileInfo.LastWriteTime
                            });
                        }
                    }
                }
            }
            catch
            {
            }

            return users;
        }

        /// <summary>
        /// Copies a profile folder to a new profile with the specified name.
        /// </summary>
        /// <param name="sourcePath">The source profile folder path.</param>
        /// <param name="newProfileName">The new profile name (without "settings_" prefix).</param>
        /// <param name="serverFolderPath">The server folder path where the new profile will be created.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool CopyProfile(string sourcePath, string newProfileName, string serverFolderPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
                return false;

            if (string.IsNullOrEmpty(newProfileName))
                return false;

            if (!SecurityValidationHelper.IsValidProcessName(newProfileName))
                return false;

            string destinationFolderName = "settings_" + newProfileName;
            string destinationPath = Path.Combine(serverFolderPath, destinationFolderName);

            if (Directory.Exists(destinationPath))
                return false;

            try
            {
                string normalizedSource = SecurityValidationHelper.ValidateAndNormalizePath(sourcePath);
                string normalizedDest = SecurityValidationHelper.ValidateAndNormalizePath(serverFolderPath);

                Directory.CreateDirectory(destinationPath);

                CopyDirectory(normalizedSource, destinationPath);

                return true;
            }
            catch
            {
                try
                {
                    if (Directory.Exists(destinationPath))
                        Directory.Delete(destinationPath, true);
                }
                catch
                {
                }
                return false;
            }
        }

        /// <summary>
        /// Copies character and/or user files for the selected profile, limited to known entries.
        /// </summary>
        /// <param name="profilePath">The profile folder path.</param>
        /// <param name="sourceCharacter">The source character file to copy.</param>
        /// <param name="sourceUser">The source user file to copy.</param>
        /// <param name="copyCharacterFiles">Whether to copy character files.</param>
        /// <param name="copyUserFiles">Whether to copy user files.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool CopyCharacterAndUserFiles(string profilePath, EveOnlineCharacter sourceCharacter, EveOnlineUser sourceUser, bool copyCharacterFiles, bool copyUserFiles)
        {
            if (string.IsNullOrEmpty(profilePath) || !Directory.Exists(profilePath))
                return false;

            if (!copyCharacterFiles && !copyUserFiles)
                return false;

            if (copyCharacterFiles && (sourceCharacter == null || string.IsNullOrEmpty(sourceCharacter.FilePath) || !File.Exists(sourceCharacter.FilePath)))
                return false;

            if (copyUserFiles && (sourceUser == null || string.IsNullOrEmpty(sourceUser.FilePath) || !File.Exists(sourceUser.FilePath)))
                return false;

            try
            {
                string normalizedProfile = SecurityValidationHelper.ValidateAndNormalizePath(profilePath);

                if (copyCharacterFiles)
                {
                    string normalizedCharSource = SecurityValidationHelper.ValidateAndNormalizePath(sourceCharacter!.FilePath);
                    IEnumerable<EveOnlineCharacter> targetCharacters = GetProfileCharacters(normalizedProfile)
                        .Where(c => !string.IsNullOrEmpty(c.FilePath) &&
                                    !c.FilePath.Equals(normalizedCharSource, StringComparison.OrdinalIgnoreCase));

                    foreach (EveOnlineCharacter? target in targetCharacters)
                    {
                        string normalizedTarget = SecurityValidationHelper.ValidateAndNormalizePath(target.FilePath);
                        File.Copy(normalizedCharSource, normalizedTarget, overwrite: true);
                    }
                }

                if (copyUserFiles)
                {
                    string normalizedUserSource = SecurityValidationHelper.ValidateAndNormalizePath(sourceUser!.FilePath);
                    IEnumerable<EveOnlineUser> targetUsers = GetProfileUsers(normalizedProfile)
                        .Where(u => !string.IsNullOrEmpty(u.FilePath) &&
                                    !u.FilePath.Equals(normalizedUserSource, StringComparison.OrdinalIgnoreCase));

                    foreach (EveOnlineUser? target in targetUsers)
                    {
                        string normalizedTarget = SecurityValidationHelper.ValidateAndNormalizePath(target.FilePath);
                        File.Copy(normalizedUserSource, normalizedTarget, overwrite: true);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? ExtractIdFromFileName(string fileName, string prefix)
        {
            string idPart = fileName.Substring(prefix.Length);
            if (idPart.Contains('.'))
            {
                idPart = idPart.Substring(0, idPart.LastIndexOf('.'));
            }
            return idPart.Trim();
        }

        private bool IsValidNumericId(string idPart)
        {
            return !string.IsNullOrWhiteSpace(idPart) &&
                   !idPart.Contains('(') &&
                   !idPart.Contains(')') &&
                   !idPart.Contains(',') &&
                   !idPart.Equals("(unset)", StringComparison.OrdinalIgnoreCase) &&
                   idPart != "_" &&
                   !idPart.All(c => c == '_') &&
                   long.TryParse(idPart, out _);
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, false);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        /// <summary>
        /// Fetches character name from EVE ESI API.
        /// </summary>
        /// <param name="characterId">The character ID.</param>
        /// <returns>The character name, or null if not found or on error.</returns>
        public async Task<string?> GetCharacterNameFromEsiAsync(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return null;

            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "YAEP-EVE-Profile-Manager");

                string apiUrl = $"https://esi.evetech.net/latest/characters/{characterId}/?datasource=tranquility";
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch character name: {response.StatusCode}");
                    return null;
                }

                EsiCharacterResponse? characterData = await response.Content.ReadFromJsonAsync<EsiCharacterResponse>();
                return characterData?.Name;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching character name from ESI: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the EVE Online client is currently running.
        /// </summary>
        /// <returns>True if EVE Online is running, false otherwise.</returns>
        public bool IsEveOnlineRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("exefile");
                if (processes.Length > 0)
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// EVE ESI API character response model.
    /// </summary>
    internal class EsiCharacterResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
