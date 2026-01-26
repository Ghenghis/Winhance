using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for Quick Look (spacebar preview) functionality.
    /// </summary>
    public interface IQuickLookService
    {
        /// <summary>
        /// Gets whether Quick Look is currently showing.
        /// </summary>
        bool IsShowing { get; }

        /// <summary>
        /// Gets the currently previewed file path.
        /// </summary>
        string? CurrentFilePath { get; }

        /// <summary>
        /// Gets or sets whether Quick Look is enabled.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Event raised when Quick Look state changes.
        /// </summary>
        event EventHandler<QuickLookStateEventArgs>? StateChanged;

        /// <summary>
        /// Shows Quick Look for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="previewImage">Preview image stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ShowQuickLookAsync(string filePath, Stream? previewImage = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hides Quick Look.
        /// </summary>
        void Hide();

        /// <summary>
        /// Toggles Quick Look visibility.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        Task ToggleAsync(string filePath);

        /// <summary>
        /// Navigates to the next file in Quick Look.
        /// </summary>
        /// <param name="files">List of files to navigate.</param>
        Task NavigateNextAsync(string[] files);

        /// <summary>
        /// Navigates to the previous file in Quick Look.
        /// </summary>
        /// <param name="files">List of files to navigate.</param>
        Task NavigatePreviousAsync(string[] files);

        /// <summary>
        /// Enters full-screen mode.
        /// </summary>
        void EnterFullScreen();

        /// <summary>
        /// Exits full-screen mode.
        /// </summary>
        void ExitFullScreen();

        /// <summary>
        /// Toggles full-screen mode.
        /// </summary>
        void ToggleFullScreen();

        /// <summary>
        /// Zooms in on the preview.
        /// </summary>
        void ZoomIn();

        /// <summary>
        /// Zooms out on the preview.
        /// </summary>
        void ZoomOut();

        /// <summary>
        /// Resets zoom to fit.
        /// </summary>
        void ZoomToFit();

        /// <summary>
        /// Sets the zoom level.
        /// </summary>
        /// <param name="zoomLevel">Zoom level (1.0 = 100%).</param>
        void SetZoom(double zoomLevel);

        /// <summary>
        /// Gets the current zoom level.
        /// </summary>
        double GetZoom();

        /// <summary>
        /// Opens the file with the default application.
        /// </summary>
        void OpenWithDefaultApp();

        /// <summary>
        /// Starts slideshow for image files.
        /// </summary>
        /// <param name="files">Image files to show.</param>
        /// <param name="intervalSeconds">Interval between slides.</param>
        Task StartSlideshowAsync(string[] files, int intervalSeconds = 3);

        /// <summary>
        /// Stops the slideshow.
        /// </summary>
        void StopSlideshow();

        /// <summary>
        /// Gets Quick Look content for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Quick Look content.</returns>
        Task<QuickLookContent> GetContentAsync(string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Quick Look content result.
    /// </summary>
    public class QuickLookContent
    {
        /// <summary>Gets or sets the content type.</summary>
        public QuickLookContentType ContentType { get; set; }

        /// <summary>Gets or sets the image source (for images/videos).</summary>
        public Stream? ImageStream { get; set; }

        /// <summary>Gets or sets the text content (for text/code).</summary>
        public string? TextContent { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the file name.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Gets or sets the file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets the file size display string.</summary>
        public string FileSizeDisplay { get; set; } = string.Empty;

        /// <summary>Gets or sets additional info lines.</summary>
        public string[] InfoLines { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets whether content can be played (audio/video).</summary>
        public bool CanPlay { get; set; }

        /// <summary>Gets or sets the duration (for audio/video).</summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>Gets or sets the natural width (for images/videos).</summary>
        public int? NaturalWidth { get; set; }

        /// <summary>Gets or sets the natural height (for images/videos).</summary>
        public int? NaturalHeight { get; set; }

        /// <summary>Gets or sets the error message if loading failed.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Quick Look content types.
    /// </summary>
    public enum QuickLookContentType
    {
        /// <summary>Unknown/unsupported type.</summary>
        Unknown,

        /// <summary>Image that can be displayed.</summary>
        Image,

        /// <summary>Video that can be played.</summary>
        Video,

        /// <summary>Audio that can be played.</summary>
        Audio,

        /// <summary>Plain text.</summary>
        Text,

        /// <summary>Code with syntax highlighting.</summary>
        Code,

        /// <summary>PDF document.</summary>
        Pdf,

        /// <summary>Markdown document.</summary>
        Markdown,

        /// <summary>Archive contents.</summary>
        Archive,

        /// <summary>File icon only (no preview).</summary>
        IconOnly,

        /// <summary>Error loading content.</summary>
        Error
    }

    /// <summary>
    /// Event args for Quick Look state changes.
    /// </summary>
    public class QuickLookStateEventArgs : EventArgs
    {
        /// <summary>Gets or sets whether Quick Look is showing.</summary>
        public bool IsShowing { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string? FilePath { get; set; }

        /// <summary>Gets or sets whether in full-screen mode.</summary>
        public bool IsFullScreen { get; set; }

        /// <summary>Gets or sets whether slideshow is active.</summary>
        public bool IsSlideshowActive { get; set; }
    }
}
