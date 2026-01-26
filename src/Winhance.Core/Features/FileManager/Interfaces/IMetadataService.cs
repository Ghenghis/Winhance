using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for reading and writing file metadata (EXIF, ID3, etc.).
    /// </summary>
    public interface IMetadataService
    {
        /// <summary>
        /// Gets metadata for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File metadata.</returns>
        Task<ExtendedMetadata> GetMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets image metadata (EXIF).
        /// </summary>
        /// <param name="filePath">Path to the image.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Image metadata.</returns>
        Task<ImageExifMetadata> GetImageMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets audio metadata (ID3, etc.).
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Audio metadata.</returns>
        Task<AudioTagMetadata> GetAudioMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets video metadata.
        /// </summary>
        /// <param name="filePath">Path to the video file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Video metadata.</returns>
        Task<VideoFileMetadata> GetVideoMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets document metadata.
        /// </summary>
        /// <param name="filePath">Path to the document.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Document metadata.</returns>
        Task<DocumentMetadata> GetDocumentMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets audio metadata.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="metadata">Metadata to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetAudioMetadataAsync(string filePath, AudioTagMetadata metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes metadata from a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="metadataTypes">Types of metadata to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RemoveMetadataAsync(string filePath, MetadataTypes metadataTypes = MetadataTypes.All, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies metadata from one file to another.
        /// </summary>
        /// <param name="sourcePath">Source file path.</param>
        /// <param name="destPath">Destination file path.</param>
        /// <param name="metadataTypes">Types of metadata to copy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CopyMetadataAsync(string sourcePath, string destPath, MetadataTypes metadataTypes = MetadataTypes.All, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available metadata fields for a file type.
        /// </summary>
        /// <param name="fileExtension">File extension.</param>
        /// <returns>Available metadata fields.</returns>
        IEnumerable<MetadataField> GetAvailableFields(string fileExtension);

        /// <summary>
        /// Checks if a file type supports metadata.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>True if metadata is supported.</returns>
        bool SupportsMetadata(string filePath);
    }

    /// <summary>
    /// Types of metadata.
    /// </summary>
    [Flags]
    public enum MetadataTypes
    {
        /// <summary>No metadata.</summary>
        None = 0,

        /// <summary>EXIF data for images.</summary>
        Exif = 1,

        /// <summary>IPTC data for images.</summary>
        Iptc = 2,

        /// <summary>XMP data.</summary>
        Xmp = 4,

        /// <summary>ID3 tags for audio.</summary>
        Id3 = 8,

        /// <summary>Document properties.</summary>
        Document = 16,

        /// <summary>GPS location data.</summary>
        Gps = 32,

        /// <summary>All metadata types.</summary>
        All = Exif | Iptc | Xmp | Id3 | Document | Gps
    }

    /// <summary>
    /// Extended metadata for any file type.
    /// </summary>
    public class ExtendedMetadata
    {
        /// <summary>Gets or sets the file path.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the file type.</summary>
        public MetadataFileType FileType { get; set; }

        /// <summary>Gets or sets all metadata as key-value pairs.</summary>
        public Dictionary<string, string> AllProperties { get; set; } = new();

        /// <summary>Gets or sets the image metadata.</summary>
        public ImageExifMetadata? ImageMetadata { get; set; }

        /// <summary>Gets or sets the audio metadata.</summary>
        public AudioTagMetadata? AudioMetadata { get; set; }

        /// <summary>Gets or sets the video metadata.</summary>
        public VideoFileMetadata? VideoMetadata { get; set; }

        /// <summary>Gets or sets the document metadata.</summary>
        public DocumentMetadata? DocumentMetadata { get; set; }
    }

    /// <summary>
    /// Metadata file types.
    /// </summary>
    public enum MetadataFileType
    {
        /// <summary>Unknown type.</summary>
        Unknown,

        /// <summary>Image file.</summary>
        Image,

        /// <summary>Audio file.</summary>
        Audio,

        /// <summary>Video file.</summary>
        Video,

        /// <summary>Document file.</summary>
        Document,

        /// <summary>Archive file.</summary>
        Archive
    }

    /// <summary>
    /// Image EXIF metadata.
    /// </summary>
    public class ImageExifMetadata
    {
        /// <summary>Gets or sets the width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the bit depth.</summary>
        public int BitDepth { get; set; }

        /// <summary>Gets or sets the DPI.</summary>
        public double? Dpi { get; set; }

        /// <summary>Gets or sets the color space.</summary>
        public string? ColorSpace { get; set; }

        /// <summary>Gets or sets the date taken.</summary>
        public DateTime? DateTaken { get; set; }

        /// <summary>Gets or sets the camera make.</summary>
        public string? CameraMake { get; set; }

        /// <summary>Gets or sets the camera model.</summary>
        public string? CameraModel { get; set; }

        /// <summary>Gets or sets the lens model.</summary>
        public string? LensModel { get; set; }

        /// <summary>Gets or sets the exposure time.</summary>
        public string? ExposureTime { get; set; }

        /// <summary>Gets or sets the f-number.</summary>
        public double? FNumber { get; set; }

        /// <summary>Gets or sets the ISO speed.</summary>
        public int? Iso { get; set; }

        /// <summary>Gets or sets the focal length in mm.</summary>
        public double? FocalLength { get; set; }

        /// <summary>Gets or sets the 35mm equivalent focal length.</summary>
        public double? FocalLength35mm { get; set; }

        /// <summary>Gets or sets the flash mode.</summary>
        public string? FlashMode { get; set; }

        /// <summary>Gets or sets the exposure program.</summary>
        public string? ExposureProgram { get; set; }

        /// <summary>Gets or sets the metering mode.</summary>
        public string? MeteringMode { get; set; }

        /// <summary>Gets or sets the white balance.</summary>
        public string? WhiteBalance { get; set; }

        /// <summary>Gets or sets the orientation.</summary>
        public int? Orientation { get; set; }

        /// <summary>Gets or sets the GPS latitude.</summary>
        public double? GpsLatitude { get; set; }

        /// <summary>Gets or sets the GPS longitude.</summary>
        public double? GpsLongitude { get; set; }

        /// <summary>Gets or sets the GPS altitude.</summary>
        public double? GpsAltitude { get; set; }

        /// <summary>Gets or sets the software used.</summary>
        public string? Software { get; set; }

        /// <summary>Gets or sets the copyright.</summary>
        public string? Copyright { get; set; }

        /// <summary>Gets or sets the artist/author.</summary>
        public string? Artist { get; set; }

        /// <summary>Gets or sets the image description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the image title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets custom tags.</summary>
        public Dictionary<string, string> CustomTags { get; set; } = new();
    }

    /// <summary>
    /// Audio file metadata (ID3 tags).
    /// </summary>
    public class AudioTagMetadata
    {
        /// <summary>Gets or sets the title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the artist.</summary>
        public string? Artist { get; set; }

        /// <summary>Gets or sets the album.</summary>
        public string? Album { get; set; }

        /// <summary>Gets or sets the album artist.</summary>
        public string? AlbumArtist { get; set; }

        /// <summary>Gets or sets the year.</summary>
        public int? Year { get; set; }

        /// <summary>Gets or sets the track number.</summary>
        public int? TrackNumber { get; set; }

        /// <summary>Gets or sets the total tracks.</summary>
        public int? TotalTracks { get; set; }

        /// <summary>Gets or sets the disc number.</summary>
        public int? DiscNumber { get; set; }

        /// <summary>Gets or sets the total discs.</summary>
        public int? TotalDiscs { get; set; }

        /// <summary>Gets or sets the genre.</summary>
        public string? Genre { get; set; }

        /// <summary>Gets or sets the composer.</summary>
        public string? Composer { get; set; }

        /// <summary>Gets or sets the conductor.</summary>
        public string? Conductor { get; set; }

        /// <summary>Gets or sets the BPM.</summary>
        public int? Bpm { get; set; }

        /// <summary>Gets or sets the comment.</summary>
        public string? Comment { get; set; }

        /// <summary>Gets or sets the lyrics.</summary>
        public string? Lyrics { get; set; }

        /// <summary>Gets or sets the copyright.</summary>
        public string? Copyright { get; set; }

        /// <summary>Gets or sets the duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets the bit rate.</summary>
        public int BitRate { get; set; }

        /// <summary>Gets or sets the sample rate.</summary>
        public int SampleRate { get; set; }

        /// <summary>Gets or sets the channels.</summary>
        public int Channels { get; set; }

        /// <summary>Gets or sets the codec.</summary>
        public string? Codec { get; set; }

        /// <summary>Gets or sets whether it has album art.</summary>
        public bool HasAlbumArt { get; set; }

        /// <summary>Gets or sets the album art bytes.</summary>
        public byte[]? AlbumArt { get; set; }
    }

    /// <summary>
    /// Video file metadata.
    /// </summary>
    public class VideoFileMetadata
    {
        /// <summary>Gets or sets the title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the width.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets the frame rate.</summary>
        public double FrameRate { get; set; }

        /// <summary>Gets or sets the video codec.</summary>
        public string? VideoCodec { get; set; }

        /// <summary>Gets or sets the audio codec.</summary>
        public string? AudioCodec { get; set; }

        /// <summary>Gets or sets the video bit rate.</summary>
        public int VideoBitRate { get; set; }

        /// <summary>Gets or sets the audio bit rate.</summary>
        public int AudioBitRate { get; set; }

        /// <summary>Gets or sets the container format.</summary>
        public string? ContainerFormat { get; set; }

        /// <summary>Gets or sets the creation date.</summary>
        public DateTime? CreationDate { get; set; }

        /// <summary>Gets or sets the description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the director.</summary>
        public string? Director { get; set; }

        /// <summary>Gets or sets the producer.</summary>
        public string? Producer { get; set; }
    }

    /// <summary>
    /// Document metadata.
    /// </summary>
    public class DocumentMetadata
    {
        /// <summary>Gets or sets the title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the author.</summary>
        public string? Author { get; set; }

        /// <summary>Gets or sets the subject.</summary>
        public string? Subject { get; set; }

        /// <summary>Gets or sets the keywords.</summary>
        public string? Keywords { get; set; }

        /// <summary>Gets or sets the creator application.</summary>
        public string? Creator { get; set; }

        /// <summary>Gets or sets the producer application.</summary>
        public string? Producer { get; set; }

        /// <summary>Gets or sets the creation date.</summary>
        public DateTime? CreationDate { get; set; }

        /// <summary>Gets or sets the modification date.</summary>
        public DateTime? ModificationDate { get; set; }

        /// <summary>Gets or sets the page count.</summary>
        public int? PageCount { get; set; }

        /// <summary>Gets or sets the word count.</summary>
        public int? WordCount { get; set; }

        /// <summary>Gets or sets the character count.</summary>
        public int? CharacterCount { get; set; }

        /// <summary>Gets or sets the company.</summary>
        public string? Company { get; set; }

        /// <summary>Gets or sets the category.</summary>
        public string? Category { get; set; }

        /// <summary>Gets or sets the comments.</summary>
        public string? Comments { get; set; }

        /// <summary>Gets or sets custom properties.</summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new();
    }

    /// <summary>
    /// A metadata field definition.
    /// </summary>
    public class MetadataField
    {
        /// <summary>Gets or sets the field name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the field type.</summary>
        public MetadataFieldType FieldType { get; set; }

        /// <summary>Gets or sets whether field is writable.</summary>
        public bool IsWritable { get; set; }

        /// <summary>Gets or sets the category.</summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Metadata field types.
    /// </summary>
    public enum MetadataFieldType
    {
        /// <summary>String value.</summary>
        String,

        /// <summary>Integer value.</summary>
        Integer,

        /// <summary>Decimal value.</summary>
        Decimal,

        /// <summary>Date value.</summary>
        DateTime,

        /// <summary>Boolean value.</summary>
        Boolean,

        /// <summary>Binary data.</summary>
        Binary
    }
}
