using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service interface for detecting and querying drive information.
    /// Provides instant-speed drive detection with real hardware data.
    /// </summary>
    public interface IDriveDetectionService
    {
        /// <summary>
        /// Gets all available drives with detailed information.
        /// </summary>
        /// <returns>Collection of drive information.</returns>
        Task<IEnumerable<DriveDetailedInfo>> GetAllDrivesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific drive.
        /// </summary>
        /// <param name="driveLetter">Drive letter (e.g., "C", "D").</param>
        /// <returns>Detailed drive information.</returns>
        Task<DriveDetailedInfo?> GetDriveInfoAsync(string driveLetter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the physical disk ID for a drive letter.
        /// </summary>
        /// <param name="driveLetter">Drive letter.</param>
        /// <returns>Physical disk ID (e.g., "DISK0", "DISK1").</returns>
        Task<string?> GetPhysicalDiskIdAsync(string driveLetter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes drive cache to get latest information.
        /// </summary>
        Task RefreshDriveCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recommended transfer settings for a drive pair (source -> destination).
        /// </summary>
        /// <param name="sourceDrive">Source drive letter.</param>
        /// <param name="destinationDrive">Destination drive letter.</param>
        /// <returns>Optimal transfer settings.</returns>
        Task<DriveTransferSettings> GetOptimalTransferSettingsAsync(
            string sourceDrive,
            string destinationDrive,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests read/write speed of a drive.
        /// </summary>
        /// <param name="driveLetter">Drive letter to test.</param>
        /// <param name="testSizeMB">Size of test file in MB (default 100).</param>
        /// <returns>Speed test results.</returns>
        Task<DriveSpeedTestResult> TestDriveSpeedAsync(
            string driveLetter,
            int testSizeMB = 100,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Detailed drive information including hardware specs.
    /// </summary>
    public class DriveDetailedInfo
    {
        /// <summary>
        /// Gets or sets the drive letter (e.g., "C", "D").
        /// </summary>
        public string DriveLetter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the volume label.
        /// </summary>
        public string VolumeLabel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the physical disk ID (e.g., "\\.\PHYSICALDRIVE0").
        /// </summary>
        public string PhysicalDiskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the disk number (0, 1, 2, etc.).
        /// </summary>
        public int DiskNumber { get; set; }

        /// <summary>
        /// Gets or sets the drive type (NVMe, SATA SSD, HDD, USB, etc.).
        /// </summary>
        public DriveMediaType MediaType { get; set; }

        /// <summary>
        /// Gets or sets the bus type (NVMe, SATA, USB, etc.).
        /// </summary>
        public DriveBusType BusType { get; set; }

        /// <summary>
        /// Gets or sets the device model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the device serial number.
        /// </summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the firmware version.
        /// </summary>
        public string FirmwareVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total capacity in bytes.
        /// </summary>
        public long TotalCapacity { get; set; }

        /// <summary>
        /// Gets or sets the free space in bytes.
        /// </summary>
        public long FreeSpace { get; set; }

        /// <summary>
        /// Gets or sets the used space in bytes.
        /// </summary>
        public long UsedSpace => TotalCapacity - FreeSpace;

        /// <summary>
        /// Gets the usage percentage.
        /// </summary>
        public double UsagePercentage => TotalCapacity > 0 ? (UsedSpace * 100.0) / TotalCapacity : 0;

        /// <summary>
        /// Gets or sets the file system type (NTFS, FAT32, exFAT, etc.).
        /// </summary>
        public string FileSystem { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum theoretical read speed in MB/s.
        /// </summary>
        public double MaxReadSpeedMBps { get; set; }

        /// <summary>
        /// Gets or sets the maximum theoretical write speed in MB/s.
        /// </summary>
        public double MaxWriteSpeedMBps { get; set; }

        /// <summary>
        /// Gets or sets the measured sequential read speed in MB/s.
        /// </summary>
        public double MeasuredReadSpeedMBps { get; set; }

        /// <summary>
        /// Gets or sets the measured sequential write speed in MB/s.
        /// </summary>
        public double MeasuredWriteSpeedMBps { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a system drive.
        /// </summary>
        public bool IsSystemDrive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the drive is removable.
        /// </summary>
        public bool IsRemovable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the drive is ready.
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Gets or sets the drive health status.
        /// </summary>
        public DriveHealthStatus HealthStatus { get; set; }

        /// <summary>
        /// Gets or sets the temperature in Celsius (if available).
        /// </summary>
        public int? TemperatureCelsius { get; set; }

        /// <summary>
        /// Gets or sets the partition style (MBR, GPT).
        /// </summary>
        public string PartitionStyle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of partitions on the physical disk.
        /// </summary>
        public int PartitionCount { get; set; }

        /// <summary>
        /// Gets or sets when the drive was last checked.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets a human-readable display of total capacity.
        /// </summary>
        public string TotalCapacityDisplay => FormatBytes(TotalCapacity);

        /// <summary>
        /// Gets a human-readable display of free space.
        /// </summary>
        public string FreeSpaceDisplay => FormatBytes(FreeSpace);

        /// <summary>
        /// Gets the drive type description.
        /// </summary>
        public string TypeDescription => GetTypeDescription();

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private string GetTypeDescription()
        {
            var type = MediaType switch
            {
                DriveMediaType.NVMe => "NVMe SSD",
                DriveMediaType.SSD => "SATA SSD",
                DriveMediaType.HDD => "HDD",
                DriveMediaType.USB => "USB Drive",
                DriveMediaType.SDCard => "SD Card",
                DriveMediaType.Network => "Network Drive",
                DriveMediaType.Virtual => "Virtual Drive",
                _ => "Unknown",
            };

            var bus = BusType switch
            {
                DriveBusType.NVMe => " (NVMe)",
                DriveBusType.SATA => " (SATA)",
                DriveBusType.USB2 => " (USB 2.0)",
                DriveBusType.USB3 => " (USB 3.0)",
                DriveBusType.USB3Gen2 => " (USB 3.2 Gen2)",
                DriveBusType.Thunderbolt => " (Thunderbolt)",
                _ => string.Empty,
            };

            return type + bus;
        }
    }

    /// <summary>
    /// Drive media type classification.
    /// </summary>
    public enum DriveMediaType
    {
        Unknown = 0,
        HDD = 1,
        SSD = 2,
        NVMe = 3,
        USB = 4,
        SDCard = 5,
        Network = 6,
        Virtual = 7,
        Optical = 8,
    }

    /// <summary>
    /// Drive bus type classification.
    /// </summary>
    public enum DriveBusType
    {
        Unknown = 0,
        SATA = 1,
        NVMe = 2,
        USB2 = 3,
        USB3 = 4,
        USB3Gen2 = 5,
        Thunderbolt = 6,
        SCSI = 7,
        SAS = 8,
        IDE = 9,
        Network = 10,
        Virtual = 11,
    }

    /// <summary>
    /// Drive health status.
    /// </summary>
    public enum DriveHealthStatus
    {
        Unknown = 0,
        Healthy = 1,
        Warning = 2,
        Critical = 3,
        Failed = 4,
    }

    /// <summary>
    /// Optimal transfer settings for file operations between drives.
    /// </summary>
    public class DriveTransferSettings
    {
        /// <summary>
        /// Gets or sets the recommended buffer size in bytes.
        /// </summary>
        public int BufferSize { get; set; } = 4 * 1024 * 1024; // 4MB default

        /// <summary>
        /// Gets or sets the number of parallel copy operations.
        /// </summary>
        public int ParallelOperations { get; set; } = 4;

        /// <summary>
        /// Gets or sets a value indicating whether to use async I/O.
        /// </summary>
        public bool UseAsyncIO { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to use direct I/O (bypass OS cache).
        /// </summary>
        public bool UseDirectIO { get; set; }

        /// <summary>
        /// Gets or sets the estimated transfer speed in MB/s.
        /// </summary>
        public double EstimatedSpeedMBps { get; set; }

        /// <summary>
        /// Gets or sets the description of the transfer settings.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Results of a drive speed test.
    /// </summary>
    public class DriveSpeedTestResult
    {
        /// <summary>
        /// Gets or sets the drive letter tested.
        /// </summary>
        public string DriveLetter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sequential read speed in MB/s.
        /// </summary>
        public double SequentialReadMBps { get; set; }

        /// <summary>
        /// Gets or sets the sequential write speed in MB/s.
        /// </summary>
        public double SequentialWriteMBps { get; set; }

        /// <summary>
        /// Gets or sets the random read speed in MB/s.
        /// </summary>
        public double RandomReadMBps { get; set; }

        /// <summary>
        /// Gets or sets the random write speed in MB/s.
        /// </summary>
        public double RandomWriteMBps { get; set; }

        /// <summary>
        /// Gets or sets the test duration.
        /// </summary>
        public TimeSpan TestDuration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the test was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if test failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
