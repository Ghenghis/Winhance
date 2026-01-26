using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// High-performance drive detection service with real hardware data.
    /// Uses WMI and native Win32 APIs for instant drive information.
    /// </summary>
    public class DriveDetectionService : IDriveDetectionService
    {
        private readonly ILogService _logService;
        private readonly ConcurrentDictionary<string, DriveDetailedInfo> _driveCache;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

        public DriveDetectionService(ILogService logService)
        {
            _logService = logService;
            _driveCache = new ConcurrentDictionary<string, DriveDetailedInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IEnumerable<DriveDetailedInfo>> GetAllDrivesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheRefreshedAsync(cancellationToken).ConfigureAwait(false);
            return _driveCache.Values.OrderBy(d => d.DriveLetter).ToList();
        }

        public async Task<DriveDetailedInfo?> GetDriveInfoAsync(string driveLetter, CancellationToken cancellationToken = default)
        {
            driveLetter = NormalizeDriveLetter(driveLetter);
            await EnsureCacheRefreshedAsync(cancellationToken).ConfigureAwait(false);
            return _driveCache.TryGetValue(driveLetter, out var info) ? info : null;
        }

        public async Task<string?> GetPhysicalDiskIdAsync(string driveLetter, CancellationToken cancellationToken = default)
        {
            var driveInfo = await GetDriveInfoAsync(driveLetter, cancellationToken).ConfigureAwait(false);
            return driveInfo?.PhysicalDiskId;
        }

        public async Task RefreshDriveCacheAsync(CancellationToken cancellationToken = default)
        {
            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _driveCache.Clear();
                await PopulateDriveCacheAsync(cancellationToken).ConfigureAwait(false);
                _lastCacheRefresh = DateTime.UtcNow;
                _logService.Log(LogLevel.Debug, $"Drive cache refreshed with {_driveCache.Count} drives");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<DriveTransferSettings> GetOptimalTransferSettingsAsync(
            string sourceDrive,
            string destinationDrive,
            CancellationToken cancellationToken = default)
        {
            var source = await GetDriveInfoAsync(sourceDrive, cancellationToken).ConfigureAwait(false);
            var dest = await GetDriveInfoAsync(destinationDrive, cancellationToken).ConfigureAwait(false);

            var settings = new DriveTransferSettings();

            if (source == null || dest == null)
            {
                settings.Description = "Unable to detect drive types, using default settings";
                return settings;
            }

            // Determine optimal settings based on drive types
            var sourceIsNVMe = source.MediaType == DriveMediaType.NVMe;
            var destIsNVMe = dest.MediaType == DriveMediaType.NVMe;
            var sourceIsSSD = source.MediaType == DriveMediaType.SSD || sourceIsNVMe;
            var destIsSSD = dest.MediaType == DriveMediaType.SSD || destIsNVMe;

            // Buffer size optimization
            if (sourceIsNVMe && destIsNVMe)
            {
                // NVMe to NVMe: Large buffers, max parallelism
                settings.BufferSize = 16 * 1024 * 1024; // 16MB
                settings.ParallelOperations = 8;
                settings.UseDirectIO = true;
                settings.EstimatedSpeedMBps = Math.Min(source.MaxReadSpeedMBps, dest.MaxWriteSpeedMBps);
                settings.Description = "NVMe to NVMe: Maximum performance mode";
            }
            else if (sourceIsSSD && destIsSSD)
            {
                // SSD to SSD: Large buffers, high parallelism
                settings.BufferSize = 8 * 1024 * 1024; // 8MB
                settings.ParallelOperations = 6;
                settings.UseDirectIO = true;
                settings.EstimatedSpeedMBps = Math.Min(source.MaxReadSpeedMBps, dest.MaxWriteSpeedMBps) * 0.8;
                settings.Description = "SSD to SSD: High performance mode";
            }
            else if (source.MediaType == DriveMediaType.HDD || dest.MediaType == DriveMediaType.HDD)
            {
                // HDD involved: Sequential access is key
                settings.BufferSize = 4 * 1024 * 1024; // 4MB
                settings.ParallelOperations = 2; // Limit parallelism for HDD
                settings.UseDirectIO = false;
                settings.EstimatedSpeedMBps = 150; // Typical HDD speed
                settings.Description = "HDD involved: Sequential optimization mode";
            }
            else if (source.BusType == DriveBusType.USB3Gen2 || dest.BusType == DriveBusType.USB3Gen2)
            {
                // USB 3.2 Gen2
                settings.BufferSize = 4 * 1024 * 1024; // 4MB
                settings.ParallelOperations = 4;
                settings.UseDirectIO = false;
                settings.EstimatedSpeedMBps = 1000; // USB 3.2 Gen2 max
                settings.Description = "USB 3.2 Gen2: Optimized for high-speed USB";
            }
            else if (source.BusType == DriveBusType.USB3 || dest.BusType == DriveBusType.USB3)
            {
                // USB 3.0
                settings.BufferSize = 2 * 1024 * 1024; // 2MB
                settings.ParallelOperations = 3;
                settings.UseDirectIO = false;
                settings.EstimatedSpeedMBps = 400; // USB 3.0 typical
                settings.Description = "USB 3.0: Optimized for USB 3.0";
            }
            else
            {
                // Default
                settings.BufferSize = 1 * 1024 * 1024; // 1MB
                settings.ParallelOperations = 2;
                settings.UseDirectIO = false;
                settings.EstimatedSpeedMBps = 50;
                settings.Description = "Default transfer settings";
            }

            settings.UseAsyncIO = true;
            return settings;
        }

        public async Task<DriveSpeedTestResult> TestDriveSpeedAsync(
            string driveLetter,
            int testSizeMB = 100,
            CancellationToken cancellationToken = default)
        {
            driveLetter = NormalizeDriveLetter(driveLetter);
            var result = new DriveSpeedTestResult { DriveLetter = driveLetter };
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var testPath = Path.Combine($"{driveLetter}:\\", $"winhance_speedtest_{Guid.NewGuid():N}.tmp");
                var testData = new byte[testSizeMB * 1024 * 1024];

                // Generate random data
                new Random().NextBytes(testData);

                // Write test
                var writeStart = stopwatch.ElapsedMilliseconds;
                await using (var fs = new FileStream(testPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous))
                {
                    await fs.WriteAsync(testData, cancellationToken).ConfigureAwait(false);
                    await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                var writeEnd = stopwatch.ElapsedMilliseconds;
                var writeTimeSeconds = (writeEnd - writeStart) / 1000.0;
                result.SequentialWriteMBps = testSizeMB / writeTimeSeconds;

                // Read test
                var readStart = stopwatch.ElapsedMilliseconds;
                await using (var fs = new FileStream(testPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous))
                {
                    var buffer = new byte[testSizeMB * 1024 * 1024];
                    await fs.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }

                var readEnd = stopwatch.ElapsedMilliseconds;
                var readTimeSeconds = (readEnd - readStart) / 1000.0;
                result.SequentialReadMBps = testSizeMB / readTimeSeconds;

                // Cleanup
                File.Delete(testPath);

                result.Success = true;
                result.TestDuration = stopwatch.Elapsed;

                // Update cache with measured speeds
                if (_driveCache.TryGetValue(driveLetter, out var driveInfo))
                {
                    driveInfo.MeasuredReadSpeedMBps = result.SequentialReadMBps;
                    driveInfo.MeasuredWriteSpeedMBps = result.SequentialWriteMBps;
                }

                _logService.Log(LogLevel.Info,
                    $"Speed test {driveLetter}: Read={result.SequentialReadMBps:F1} MB/s, Write={result.SequentialWriteMBps:F1} MB/s");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logService.Log(LogLevel.Error, $"Speed test failed for {driveLetter}: {ex.Message}");
            }

            stopwatch.Stop();
            result.TestDuration = stopwatch.Elapsed;
            return result;
        }

        private async Task EnsureCacheRefreshedAsync(CancellationToken cancellationToken)
        {
            if (DateTime.UtcNow - _lastCacheRefresh > _cacheExpiration || _driveCache.IsEmpty)
            {
                await RefreshDriveCacheAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task PopulateDriveCacheAsync(CancellationToken cancellationToken)
        {
            await Task.Run(
                () =>
            {
                try
                {
                    // Get physical disk info via WMI
                    var physicalDisks = GetPhysicalDiskInfo();
                    var diskToPartition = GetDiskToPartitionMapping();
                    var partitionToLogical = GetPartitionToLogicalMapping();

                    // Get logical drives
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var driveLetter = drive.Name.TrimEnd('\\', ':').ToUpperInvariant();
                            var info = new DriveDetailedInfo
                            {
                                DriveLetter = driveLetter,
                                IsReady = drive.IsReady,
                            };

                            if (drive.IsReady)
                            {
                                info.VolumeLabel = drive.VolumeLabel;
                                info.TotalCapacity = drive.TotalSize;
                                info.FreeSpace = drive.AvailableFreeSpace;
                                info.FileSystem = drive.DriveFormat;
                                info.IsSystemDrive = driveLetter.Equals(
                                    Environment.GetFolderPath(Environment.SpecialFolder.System).Substring(0, 1),
                                    StringComparison.OrdinalIgnoreCase);

                                // Map to physical disk
                                var diskIndex = GetPhysicalDiskIndex(driveLetter, partitionToLogical, diskToPartition);
                                if (diskIndex >= 0 && physicalDisks.TryGetValue(diskIndex, out var physicalDisk))
                                {
                                    info.DiskNumber = diskIndex;
                                    info.PhysicalDiskId = $"\\\\.\\PHYSICALDRIVE{diskIndex}";
                                    info.Model = physicalDisk.Model;
                                    info.SerialNumber = physicalDisk.SerialNumber;
                                    info.FirmwareVersion = physicalDisk.FirmwareVersion;
                                    info.MediaType = physicalDisk.MediaType;
                                    info.BusType = physicalDisk.BusType;
                                    info.PartitionStyle = physicalDisk.PartitionStyle;
                                    info.PartitionCount = physicalDisk.PartitionCount;
                                    info.MaxReadSpeedMBps = GetEstimatedReadSpeed(physicalDisk.MediaType, physicalDisk.BusType);
                                    info.MaxWriteSpeedMBps = GetEstimatedWriteSpeed(physicalDisk.MediaType, physicalDisk.BusType);
                                }

                                // Check if removable
                                info.IsRemovable = drive.DriveType == DriveType.Removable ||
                                                   info.BusType == DriveBusType.USB2 ||
                                                   info.BusType == DriveBusType.USB3 ||
                                                   info.BusType == DriveBusType.USB3Gen2;
                            }

                            info.LastChecked = DateTime.UtcNow;
                            _driveCache[driveLetter] = info;
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Warning, $"Error getting info for drive {drive.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error populating drive cache: {ex.Message}");
                }
            }, cancellationToken);
        }

        private static Dictionary<int, PhysicalDiskData> GetPhysicalDiskInfo()
        {
            var disks = new Dictionary<int, PhysicalDiskData>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject disk in searcher.Get())
                {
                    try
                    {
                        var deviceId = disk["DeviceID"]?.ToString() ?? string.Empty;
                        if (!int.TryParse(
                            deviceId.Replace("\\\\.\\PHYSICALDRIVE", string.Empty, StringComparison.OrdinalIgnoreCase),
                            out var diskIndex))
                        {
                            continue;
                        }

                        var mediaType = disk["MediaType"]?.ToString() ?? string.Empty;
                        var interfaceType = disk["InterfaceType"]?.ToString() ?? string.Empty;
                        var model = disk["Model"]?.ToString() ?? string.Empty;

                        disks[diskIndex] = new PhysicalDiskData
                        {
                            Index = diskIndex,
                            Model = model.Trim(),
                            SerialNumber = disk["SerialNumber"]?.ToString()?.Trim() ?? string.Empty,
                            FirmwareVersion = disk["FirmwareRevision"]?.ToString()?.Trim() ?? string.Empty,
                            MediaType = DetectMediaType(model, mediaType, interfaceType),
                            BusType = DetectBusType(interfaceType, model),
                            PartitionStyle = disk["PartitionStyle"]?.ToString() ?? "Unknown",
                            PartitionCount = Convert.ToInt32(disk["Partitions"] ?? 0),
                        };
                    }
                    catch
                    {
                        // Skip individual disk errors
                    }
                }
            }
            catch
            {
                // WMI may not be available
            }

            return disks;
        }

        private static Dictionary<string, int> GetDiskToPartitionMapping()
        {
            var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskDriveToDiskPartition");
                foreach (ManagementObject item in searcher.Get())
                {
                    try
                    {
                        var antecedent = item["Antecedent"]?.ToString() ?? string.Empty;
                        var dependent = item["Dependent"]?.ToString() ?? string.Empty;

                        // Extract disk index from antecedent
                        var diskMatch = System.Text.RegularExpressions.Regex.Match(
                            antecedent, @"PHYSICALDRIVE(\d+)");
                        if (!diskMatch.Success)
                        {
                            continue;
                        }

                        var diskIndex = int.Parse(diskMatch.Groups[1].Value);

                        // Extract partition ID from dependent
                        var partMatch = System.Text.RegularExpressions.Regex.Match(
                            dependent, @"Disk #(\d+), Partition #(\d+)");
                        if (partMatch.Success)
                        {
                            var partitionId = $"Disk #{partMatch.Groups[1].Value}, Partition #{partMatch.Groups[2].Value}";
                            mapping[partitionId] = diskIndex;
                        }
                    }
                    catch
                    {
                        // Skip errors
                    }
                }
            }
            catch
            {
                // WMI may not be available
            }

            return mapping;
        }

        private static Dictionary<string, string> GetPartitionToLogicalMapping()
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_LogicalDiskToPartition");
                foreach (ManagementObject item in searcher.Get())
                {
                    try
                    {
                        var antecedent = item["Antecedent"]?.ToString() ?? string.Empty;
                        var dependent = item["Dependent"]?.ToString() ?? string.Empty;

                        // Extract partition ID
                        var partMatch = System.Text.RegularExpressions.Regex.Match(
                            antecedent, @"Disk #(\d+), Partition #(\d+)");
                        if (!partMatch.Success)
                        {
                            continue;
                        }

                        var partitionId = $"Disk #{partMatch.Groups[1].Value}, Partition #{partMatch.Groups[2].Value}";

                        // Extract drive letter
                        var driveMatch = System.Text.RegularExpressions.Regex.Match(dependent, @"DeviceID=""([A-Z]):""");
                        if (driveMatch.Success)
                        {
                            mapping[driveMatch.Groups[1].Value] = partitionId;
                        }
                    }
                    catch
                    {
                        // Skip errors
                    }
                }
            }
            catch
            {
                // WMI may not be available
            }

            return mapping;
        }

        private static int GetPhysicalDiskIndex(
            string driveLetter,
            Dictionary<string, string> partitionToLogical,
            Dictionary<string, int> diskToPartition)
        {
            if (partitionToLogical.TryGetValue(driveLetter, out var partitionId))
            {
                if (diskToPartition.TryGetValue(partitionId, out var diskIndex))
                {
                    return diskIndex;
                }
            }

            return -1;
        }

        private static DriveMediaType DetectMediaType(string model, string mediaType, string interfaceType)
        {
            var modelLower = model.ToLowerInvariant();

            // Check for NVMe
            if (modelLower.Contains("nvme", StringComparison.Ordinal) ||
                interfaceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            {
                return DriveMediaType.NVMe;
            }

            // Check for SSD indicators
            if (modelLower.Contains("ssd", StringComparison.Ordinal) ||
                modelLower.Contains("solid state", StringComparison.Ordinal) ||
                modelLower.Contains("samsung 9", StringComparison.Ordinal) ||
                modelLower.Contains("crucial", StringComparison.Ordinal) ||
                modelLower.Contains("sandisk", StringComparison.Ordinal) ||
                modelLower.Contains("wd blue sn", StringComparison.Ordinal) ||
                modelLower.Contains("kingston", StringComparison.Ordinal))
            {
                return DriveMediaType.SSD;
            }

            // Check for HDD
            if (mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ||
                modelLower.Contains("hdd", StringComparison.Ordinal) ||
                modelLower.Contains("seagate", StringComparison.Ordinal) ||
                modelLower.Contains("western digital", StringComparison.Ordinal) ||
                modelLower.Contains("toshiba", StringComparison.Ordinal))
            {
                // Could be HDD or SSD - check for typical HDD model patterns
                if (modelLower.Contains("barracuda", StringComparison.Ordinal) ||
                    modelLower.Contains("ironwolf", StringComparison.Ordinal) ||
                    modelLower.Contains("wd blue", StringComparison.Ordinal) && !modelLower.Contains("sn", StringComparison.Ordinal) ||
                    modelLower.Contains("wd black", StringComparison.Ordinal) && !modelLower.Contains("sn", StringComparison.Ordinal) ||
                    modelLower.Contains("wd red", StringComparison.Ordinal))
                {
                    return DriveMediaType.HDD;
                }
            }

            // Check for removable/USB
            if (mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase))
            {
                return DriveMediaType.USB;
            }

            return DriveMediaType.Unknown;
        }

        private static DriveBusType DetectBusType(string interfaceType, string model)
        {
            var interfaceLower = interfaceType.ToLowerInvariant();
            var modelLower = model.ToLowerInvariant();

            if (interfaceLower.Contains("nvme", StringComparison.Ordinal) ||
                modelLower.Contains("nvme", StringComparison.Ordinal))
            {
                return DriveBusType.NVMe;
            }

            if (interfaceLower.Contains("usb", StringComparison.Ordinal))
            {
                // Try to detect USB version from model or interface
                if (modelLower.Contains("usb 3.2", StringComparison.Ordinal) ||
                    modelLower.Contains("usb3.2", StringComparison.Ordinal) ||
                    modelLower.Contains("10gbps", StringComparison.Ordinal))
                {
                    return DriveBusType.USB3Gen2;
                }

                if (modelLower.Contains("usb 3", StringComparison.Ordinal) ||
                    modelLower.Contains("usb3", StringComparison.Ordinal) ||
                    modelLower.Contains("5gbps", StringComparison.Ordinal))
                {
                    return DriveBusType.USB3;
                }

                return DriveBusType.USB2;
            }

            if (interfaceLower.Contains("sata", StringComparison.Ordinal) ||
                interfaceLower.Contains("ide", StringComparison.Ordinal))
            {
                return DriveBusType.SATA;
            }

            if (interfaceLower.Contains("scsi", StringComparison.Ordinal))
            {
                return DriveBusType.SCSI;
            }

            return DriveBusType.Unknown;
        }

        private static double GetEstimatedReadSpeed(DriveMediaType mediaType, DriveBusType busType)
        {
            return (mediaType, busType) switch
            {
                (DriveMediaType.NVMe, _) => 3500, // Gen4 NVMe
                (DriveMediaType.SSD, DriveBusType.SATA) => 550,
                (DriveMediaType.SSD, DriveBusType.USB3Gen2) => 1000,
                (DriveMediaType.SSD, DriveBusType.USB3) => 400,
                (DriveMediaType.HDD, _) => 150,
                (DriveMediaType.USB, DriveBusType.USB3Gen2) => 1000,
                (DriveMediaType.USB, DriveBusType.USB3) => 400,
                (DriveMediaType.USB, DriveBusType.USB2) => 40,
                _ => 100,
            };
        }

        private static double GetEstimatedWriteSpeed(DriveMediaType mediaType, DriveBusType busType)
        {
            return (mediaType, busType) switch
            {
                (DriveMediaType.NVMe, _) => 3000, // Gen4 NVMe
                (DriveMediaType.SSD, DriveBusType.SATA) => 520,
                (DriveMediaType.SSD, DriveBusType.USB3Gen2) => 900,
                (DriveMediaType.SSD, DriveBusType.USB3) => 350,
                (DriveMediaType.HDD, _) => 130,
                (DriveMediaType.USB, DriveBusType.USB3Gen2) => 900,
                (DriveMediaType.USB, DriveBusType.USB3) => 350,
                (DriveMediaType.USB, DriveBusType.USB2) => 30,
                _ => 80,
            };
        }

        private static string NormalizeDriveLetter(string driveLetter)
        {
            return driveLetter.TrimEnd('\\', ':').ToUpperInvariant();
        }

        private class PhysicalDiskData
        {
            public int Index { get; set; }

            public string Model { get; set; } = string.Empty;

            public string SerialNumber { get; set; } = string.Empty;

            public string FirmwareVersion { get; set; } = string.Empty;

            public DriveMediaType MediaType { get; set; }

            public DriveBusType BusType { get; set; }

            public string PartitionStyle { get; set; } = string.Empty;

            public int PartitionCount { get; set; }
        }
    }
}
