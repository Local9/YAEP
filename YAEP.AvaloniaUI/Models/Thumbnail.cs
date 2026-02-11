using YAEP.Shared.Interfaces;

namespace YAEP.Models
{
    /// <summary>
    /// Wrapper around IDesktopWindowManagerThumbnail that provides a convenient API for thumbnail management.
    /// Adapts the interface's Move() signature (left, top, right, bottom) to a more intuitive (x, y, width, height) signature.
    /// </summary>
    public class Thumbnail
    {
        private IDesktopWindowManagerThumbnail? _thumbnail;

        public Thumbnail()
        {
            _thumbnail = null;
        }

        public void Register(IntPtr destination, IntPtr source)
        {
            if (destination == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: Destination handle is IntPtr.Zero");
                return;
            }

            if (source == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: Source handle is IntPtr.Zero");
                return;
            }

            if (App.DesktopWindowManager == null)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: DesktopWindowManager is not available");
                return;
            }

            try
            {
                _thumbnail = App.DesktopWindowManager.GetLiveThumbnail(destination, source);
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: Successfully registered thumbnail via DesktopWindowManager");
            }
            catch (NotSupportedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: NotSupportedException - {ex.Message}");
                _thumbnail = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: Exception - {ex.GetType().Name}: {ex.Message}");
                _thumbnail = null;
            }
        }

        public void Unregister()
        {
            if (_thumbnail != null)
            {
                try
                {
                    _thumbnail.Unregister();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail.Unregister: Exception - {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    _thumbnail = null;
                }
            }
        }

        /// <summary>
        /// Moves and resizes the thumbnail. Adapts from (x, y, width, height) to (left, top, right, bottom) format.
        /// </summary>
        public void Move(int x, int y, int width, int height)
        {
            if (_thumbnail == null)
                return;

            _thumbnail.Move(x, y, x + width, y + height);
        }

        public void SetOpacity(double opacity)
        {
            if (_thumbnail == null)
                return;

            _thumbnail.SetOpacity(opacity);
        }

        public void Update()
        {
            if (_thumbnail == null)
                return;

            try
            {
                _thumbnail.Update();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Update: Exception - {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
