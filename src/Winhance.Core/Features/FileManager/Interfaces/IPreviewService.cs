using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for generating file previews - images, text, PDF, code, etc.
    /// </summary>
    public interface IPreviewService
    {
        /// <summary>
        /// Gets whether the preview pane is visible.
        /// </summary>
        bool IsPreviewVisible { get; set; }

        /// <summary>
        /// Gets or sets the preview pane position.
        /// </summary>
        PreviewPosition Position { get; set; }

        /// <summary>
        /// Gets or sets the preview pane width (when on right).
        /// </summary>
        double PreviewWidth { get; set; }

        /// <summary>
        /// Gets or sets the preview pane height (when on bottom).
        /// </summary>
        double PreviewHeight { get; set; }

        /// <summary>
        /// Event raised when preview content changes.
        /// </summary>
        event EventHandler<PreviewChangedEventArgs>? PreviewChanged;

        /// <summary>
        /// Gets the preview type for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>The preview type.</returns>
        PreviewType GetPreviewType(string filePath);

        /// <summary>
        /// Checks if a file can be previewed.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>True if preview is available.</returns>
        bool CanPreview(string filePath);

        /// <summary>
        /// Generates an image preview.
        /// </summary>
        /// <param name="filePath">Path to the image file.</param>
        /// <param name="maxWidth">Maximum preview width.</param>
        /// <param name="maxHeight">Maximum preview height.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The preview image source.</returns>
        Task<Stream?> GetImagePreviewAsync(
            string filePath,
            int maxWidth = 800,
            int maxHeight = 600,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a text preview.
        /// </summary>
        /// <param name="filePath">Path to the text file.</param>
        /// <param name="maxLines">Maximum lines to preview.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The preview text content.</returns>
        Task<TextPreviewResult> GetTextPreviewAsync(
            string filePath,
            int maxLines = 500,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a code preview with syntax highlighting.
        /// </summary>
        /// <param name="filePath">Path to the code file.</param>
        /// <param name="maxLines">Maximum lines to preview.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The code preview with highlighting info.</returns>
        Task<CodePreviewResult> GetCodePreviewAsync(
            string filePath,
            int maxLines = 500,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a video thumbnail.
        /// </summary>
        /// <param name="filePath">Path to the video file.</param>
        /// <param name="position">Position in video (default: 10%).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The video thumbnail.</returns>
        Task<Stream?> GetVideoThumbnailAsync(
            string filePath,
            TimeSpan? position = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a PDF preview.
        /// </summary>
        /// <param name="filePath">Path to the PDF file.</param>
        /// <param name="pageNumber">Page to preview (1-based).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The PDF page preview.</returns>
        Task<PdfPreviewResult> GetPdfPreviewAsync(
            string filePath,
            int pageNumber = 1,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an audio waveform preview.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="width">Waveform width.</param>
        /// <param name="height">Waveform height.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The audio waveform image.</returns>
        Task<AudioPreviewResult> GetAudioPreviewAsync(
            string filePath,
            int width = 400,
            int height = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a hex preview for binary files.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="offset">Byte offset to start.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The hex preview data.</returns>
        Task<HexPreviewResult> GetHexPreviewAsync(
            string filePath,
            long offset = 0,
            int length = 512,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets file metadata for preview.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File metadata.</returns>
        Task<FileMetadata> GetFileMetadataAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the preview cache.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Saves user preferences for preview settings.
        /// </summary>
        Task SavePreferencesAsync();

        /// <summary>
        /// Loads user preferences for preview settings.
        /// </summary>
        Task LoadPreferencesAsync();
    }

    /// <summary>
    /// Preview pane position options.
    /// </summary>
    public enum PreviewPosition
    {
        /// <summary>Preview on the right side.</summary>
        Right,

        /// <summary>Preview on the bottom.</summary>
        Bottom,

        /// <summary>Preview is hidden.</summary>
        Hidden
    }

    /// <summary>
    /// Types of file previews.
    /// </summary>
    public enum PreviewType
    {
        /// <summary>No preview available.</summary>
        None,

        /// <summary>Image preview.</summary>
        Image,

        /// <summary>Plain text preview.</summary>
        Text,

        /// <summary>Code with syntax highlighting.</summary>
        Code,

        /// <summary>Video thumbnail.</summary>
        Video,

        /// <summary>Audio waveform.</summary>
        Audio,

        /// <summary>PDF document.</summary>
        Pdf,

        /// <summary>Office document.</summary>
        Office,

        /// <summary>Markdown rendered.</summary>
        Markdown,

        /// <summary>JSON/XML tree view.</summary>
        Structured,

        /// <summary>Archive contents list.</summary>
        Archive,

        /// <summary>Hex binary view.</summary>
        Hex
    }

    /// <summary>
    /// Event args for preview changes.
    /// </summary>
    public class PreviewChangedEventArgs : EventArgs
    {
        /// <summary>Gets the file path being previewed.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Gets the preview type.</summary>
        public PreviewType PreviewType { get; set; }

        /// <summary>Gets whether preview was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Gets the error message if failed.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of text file preview.
    /// </summary>
    public class TextPreviewResult
    {
        /// <summary>Gets or sets the text content.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Gets or sets the encoding name.</summary>
        public string Encoding { get; set; } = "UTF-8";

        /// <summary>Gets or sets total line count.</summary>
        public int TotalLines { get; set; }

        /// <summary>Gets or sets lines shown in preview.</summary>
        public int PreviewedLines { get; set; }

        /// <summary>Gets or sets whether file was truncated.</summary>
        public bool IsTruncated { get; set; }

        /// <summary>Gets or sets file size in bytes.</summary>
        public long FileSize { get; set; }
    }

    /// <summary>
    /// Result of code file preview.
    /// </summary>
    public class CodePreviewResult : TextPreviewResult
    {
        /// <summary>Gets or sets the detected language.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Gets or sets syntax highlighting tokens.</summary>
        public SyntaxToken[] Tokens { get; set; } = Array.Empty<SyntaxToken>();
    }

    /// <summary>
    /// Syntax highlighting token.
    /// </summary>
    public class SyntaxToken
    {
        /// <summary>Gets or sets the start position.</summary>
        public int Start { get; set; }

        /// <summary>Gets or sets the length.</summary>
        public int Length { get; set; }

        /// <summary>Gets or sets the token type.</summary>
        public SyntaxTokenType Type { get; set; }
    }

    /// <summary>
    /// Types of syntax tokens for highlighting.
    /// </summary>
    public enum SyntaxTokenType
    {
        /// <summary>Default text.</summary>
        Default,
        /// <summary>Keyword.</summary>
        Keyword,
        /// <summary>String literal.</summary>
        String,
        /// <summary>Number literal.</summary>
        Number,
        /// <summary>Comment.</summary>
        Comment,
        /// <summary>Type name.</summary>
        Type,
        /// <summary>Function/method name.</summary>
        Function,
        /// <summary>Operator.</summary>
        Operator,
        /// <summary>Punctuation.</summary>
        Punctuation
    }

    /// <summary>
    /// Result of PDF preview.
    /// </summary>
    public class PdfPreviewResult
    {
        /// <summary>Gets or sets the page image.</summary>
        public Stream? PageImage { get; set; }

        /// <summary>Gets or sets the current page number.</summary>
        public int CurrentPage { get; set; }

        /// <summary>Gets or sets the total page count.</summary>
        public int TotalPages { get; set; }

        /// <summary>Gets or sets the page width in points.</summary>
        public double PageWidth { get; set; }

        /// <summary>Gets or sets the page height in points.</summary>
        public double PageHeight { get; set; }

        /// <summary>Gets or sets the document title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the document author.</summary>
        public string? Author { get; set; }
    }

    /// <summary>
    /// Result of audio preview.
    /// </summary>
    public class AudioPreviewResult
    {
        /// <summary>Gets or sets the waveform image.</summary>
        public Stream? WaveformImage { get; set; }

        /// <summary>Gets or sets the audio duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets the sample rate.</summary>
        public int SampleRate { get; set; }

        /// <summary>Gets or sets the bit rate in kbps.</summary>
        public int BitRate { get; set; }

        /// <summary>Gets or sets the channel count.</summary>
        public int Channels { get; set; }

        /// <summary>Gets or sets the audio format.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>Gets or sets the artist tag.</summary>
        public string? Artist { get; set; }

        /// <summary>Gets or sets the title tag.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the album tag.</summary>
        public string? Album { get; set; }

        /// <summary>Gets or sets the album art.</summary>
        public Stream? AlbumArt { get; set; }
    }

    /// <summary>
    /// Result of hex preview.
    /// </summary>
    public class HexPreviewResult
    {
        /// <summary>Gets or sets the hex dump lines.</summary>
        public HexLine[] Lines { get; set; } = Array.Empty<HexLine>();

        /// <summary>Gets or sets the start offset.</summary>
        public long StartOffset { get; set; }

        /// <summary>Gets or sets the total file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets bytes shown.</summary>
        public int BytesShown { get; set; }
    }

    /// <summary>
    /// A line in hex preview.
    /// </summary>
    public class HexLine
    {
        /// <summary>Gets or sets the offset.</summary>
        public long Offset { get; set; }

        /// <summary>Gets or sets the hex bytes.</summary>
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        /// <summary>Gets or sets the ASCII representation.</summary>
        public string Ascii { get; set; } = string.Empty;
    }

    /// <summary>
    /// File metadata for preview.
    /// </summary>
    public class FileMetadata
    {
        /// <summary>Gets or sets the file name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the size display string.</summary>
        public string SizeDisplay { get; set; } = string.Empty;

        /// <summary>Gets or sets the file type description.</summary>
        public string TypeDescription { get; set; } = string.Empty;

        /// <summary>Gets or sets the creation time.</summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>Gets or sets the modified time.</summary>
        public DateTime ModifiedTime { get; set; }

        /// <summary>Gets or sets the accessed time.</summary>
        public DateTime AccessedTime { get; set; }

        /// <summary>Gets or sets the file attributes.</summary>
        public FileAttributes Attributes { get; set; }

        /// <summary>Gets or sets whether file is read-only.</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Gets or sets whether file is hidden.</summary>
        public bool IsHidden { get; set; }

        /// <summary>Gets or sets whether file is system.</summary>
        public bool IsSystem { get; set; }

        /// <summary>Gets or sets image-specific metadata.</summary>
        public ImageMetadata? ImageInfo { get; set; }

        /// <summary>Gets or sets audio-specific metadata.</summary>
        public AudioMetadata? AudioInfo { get; set; }

        /// <summary>Gets or sets video-specific metadata.</summary>
        public VideoMetadata? VideoInfo { get; set; }
    }

    /// <summary>
    /// Image-specific metadata.
    /// </summary>
    public class ImageMetadata
    {
        /// <summary>Gets or sets the width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the bit depth.</summary>
        public int BitDepth { get; set; }

        /// <summary>Gets or sets the DPI.</summary>
        public double Dpi { get; set; }

        /// <summary>Gets or sets the color space.</summary>
        public string ColorSpace { get; set; } = string.Empty;

        /// <summary>Gets or sets the camera make (EXIF).</summary>
        public string? CameraMake { get; set; }

        /// <summary>Gets or sets the camera model (EXIF).</summary>
        public string? CameraModel { get; set; }

        /// <summary>Gets or sets the date taken (EXIF).</summary>
        public DateTime? DateTaken { get; set; }

        /// <summary>Gets or sets the exposure time.</summary>
        public string? ExposureTime { get; set; }

        /// <summary>Gets or sets the f-number.</summary>
        public string? FNumber { get; set; }

        /// <summary>Gets or sets the ISO.</summary>
        public int? Iso { get; set; }

        /// <summary>Gets or sets the focal length.</summary>
        public string? FocalLength { get; set; }

        /// <summary>Gets or sets the GPS latitude.</summary>
        public double? GpsLatitude { get; set; }

        /// <summary>Gets or sets the GPS longitude.</summary>
        public double? GpsLongitude { get; set; }
    }

    /// <summary>
    /// Audio-specific metadata.
    /// </summary>
    public class AudioMetadata
    {
        /// <summary>Gets or sets the duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets the bit rate.</summary>
        public int BitRate { get; set; }

        /// <summary>Gets or sets the sample rate.</summary>
        public int SampleRate { get; set; }

        /// <summary>Gets or sets the channels.</summary>
        public int Channels { get; set; }

        /// <summary>Gets or sets the codec.</summary>
        public string Codec { get; set; } = string.Empty;

        /// <summary>Gets or sets the title tag.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the artist tag.</summary>
        public string? Artist { get; set; }

        /// <summary>Gets or sets the album tag.</summary>
        public string? Album { get; set; }

        /// <summary>Gets or sets the year.</summary>
        public int? Year { get; set; }

        /// <summary>Gets or sets the track number.</summary>
        public int? TrackNumber { get; set; }

        /// <summary>Gets or sets the genre.</summary>
        public string? Genre { get; set; }
    }

    /// <summary>
    /// Video-specific metadata.
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>Gets or sets the duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets the width.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the frame rate.</summary>
        public double FrameRate { get; set; }

        /// <summary>Gets or sets the video codec.</summary>
        public string VideoCodec { get; set; } = string.Empty;

        /// <summary>Gets or sets the audio codec.</summary>
        public string AudioCodec { get; set; } = string.Empty;

        /// <summary>Gets or sets the video bit rate.</summary>
        public int VideoBitRate { get; set; }

        /// <summary>Gets or sets the audio bit rate.</summary>
        public int AudioBitRate { get; set; }

        /// <summary>Gets or sets the container format.</summary>
        public string ContainerFormat { get; set; } = string.Empty;
    }
}
