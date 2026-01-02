using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Event-related code for DatabaseService.
    /// </summary>
    public partial class DatabaseService
    {
    }

    /// <summary>
    /// Event arguments for profile change events.
    /// </summary>
    public class ProfileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous profile.
        /// </summary>
        public Profile? OldProfile { get; }

        /// <summary>
        /// Gets the new profile.
        /// </summary>
        public Profile? NewProfile { get; }

        /// <summary>
        /// Initializes a new instance of the ProfileChangedEventArgs class.
        /// </summary>
        /// <param name="oldProfile">The previous profile.</param>
        /// <param name="newProfile">The new profile.</param>
        public ProfileChangedEventArgs(Profile? oldProfile, Profile? newProfile)
        {
            OldProfile = oldProfile;
            NewProfile = newProfile;
        }
    }
}

