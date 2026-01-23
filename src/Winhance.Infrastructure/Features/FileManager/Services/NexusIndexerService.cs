using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Interop;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// High-performance file indexer using Rust nexus_core native library.
    /// Achieves sub-second indexing of entire drives via MFT reading.
    /// Pure performance - minimal overhead, direct native calls.
    /// </summary>
    public class NexusIndexerService : INexusIndexerService
    {
        private readonly ILogService _logService;
        private bool _initialized;
        private bool _isAvailable;
        private NexusInterop.ProgressCallback? _progressCallback;

        public bool IsAvailable => _isAvailable;
        public NexusIndexStats Stats { get; } = new();

        public event Action<ulong, ulong, string>? ProgressChanged;

        public NexusIndexerService(ILogService logService)
        {
            _logService = logService;
            _isAvailable = CheckNativeLibrary();
        }

        private bool CheckNativeLibrary()
        {
            try
            {
                // Try to load the native library
                return NexusInterop.nexus_init();
            }
            catch (DllNotFoundException)
            {
                _logService.Log(LogLevel.Warning, "nexus_core native library not found - using fallback indexer");
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to initialize nexus_core: {ex.Message}");
                return false;
            }
        }

        public bool Initialize()
        {
            if (_initialized) return true;
            if (!_isAvailable) return false;

            try
            {
                _initialized = NexusInterop.nexus_init();
                if (_initialized)
                {
                    _logService.Log(LogLevel.Info, "Nexus indexer initialized successfully");
                }
                return _initialized;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to initialize Nexus indexer: {ex.Message}");
                return false;
            }
        }

        public async Task<long> IndexAllDrivesAsync(CancellationToken cancellationToken = default)
        {
            if (!_isAvailable)
            {
                _logService.Log(LogLevel.Warning, "Nexus native library not available");
                return -1;
            }

            return await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                
                try
                {
                    _logService.Log(LogLevel.Info, "Starting MFT-based full drive indexing...");
                    
                    long count = NexusInterop.nexus_index_all();
                    
                    sw.Stop();
                    Stats.IndexTimeMs = sw.ElapsedMilliseconds;
                    Stats.TotalFiles = count > 0 ? count : 0;
                    Stats.LastIndexTime = DateTime.UtcNow;

                    if (count >= 0)
                    {
                        _logService.Log(LogLevel.Info, 
                            $"Indexed {count:N0} files in {sw.ElapsedMilliseconds}ms ({count / Math.Max(1, sw.ElapsedMilliseconds / 1000.0):N0} files/sec)");
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Indexing failed: {GetLastError()}");
                    }

                    return count;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Indexing error: {ex.Message}");
                    return -1;
                }
            }, cancellationToken);
        }

        public async Task<long> IndexDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable)
            {
                return -1;
            }

            return await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                
                try
                {
                    long count = NexusInterop.nexus_index_directory(path);
                    
                    sw.Stop();
                    
                    if (count >= 0)
                    {
                        _logService.Log(LogLevel.Info, 
                            $"Indexed {count:N0} files in '{path}' ({sw.ElapsedMilliseconds}ms)");
                    }

                    return count;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Directory indexing error: {ex.Message}");
                    return -1;
                }
            }, cancellationToken);
        }

        public async Task<IEnumerable<NexusFileEntry>> SearchAsync(string query, int maxResults = 100, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<NexusFileEntry>();
            }

            return await Task.Run(() =>
            {
                var results = new List<NexusFileEntry>();
                
                try
                {
                    long count = NexusInterop.nexus_search(query, (uint)maxResults);
                    
                    if (count > 0)
                    {
                        for (uint i = 0; i < (uint)count; i++)
                        {
                            IntPtr resultPtr = NexusInterop.nexus_get_search_result(i);
                            if (resultPtr != IntPtr.Zero)
                            {
                                var ffiResult = Marshal.PtrToStructure<FfiSearchResult>(resultPtr);
                                
                                results.Add(new NexusFileEntry
                                {
                                    Path = ffiResult.GetPath(),
                                    Name = ffiResult.GetName(),
                                    Size = (long)ffiResult.Size,
                                    IsDirectory = ffiResult.IsDir
                                });
                                
                                NexusInterop.nexus_free_result(resultPtr);
                            }
                        }
                        
                        _logService.Log(LogLevel.Debug, $"Search '{query}' returned {count} results");
                    }
                    
                    NexusInterop.nexus_clear_search_results();
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Search error: {ex.Message}");
                }

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Compute quick hash for duplicate detection (xxHash3 - very fast).
        /// </summary>
        public ulong GetQuickHash(string path)
        {
            if (!_isAvailable) return 0;
            return NexusInterop.nexus_hash_file_quick(path);
        }

        /// <summary>
        /// Compute full SHA-256 hash for verification.
        /// </summary>
        public string? GetFullHash(string path)
        {
            if (!_isAvailable) return null;
            
            IntPtr hashPtr = NexusInterop.nexus_hash_file_full(path);
            if (hashPtr == IntPtr.Zero) return null;
            
            string hash = Marshal.PtrToStringAnsi(hashPtr) ?? string.Empty;
            NexusInterop.nexus_free_string(hashPtr);
            return hash;
        }

        /// <summary>
        /// Find potential duplicate file groups by size.
        /// </summary>
        public long FindDuplicates(ulong minSize = 1024)
        {
            if (!_isAvailable) return -1;
            return NexusInterop.nexus_find_duplicates(minSize);
        }

        /// <summary>
        /// Get current index statistics from native library.
        /// </summary>
        public FfiIndexStats GetNativeStats()
        {
            if (!_isAvailable) return default;
            return NexusInterop.nexus_get_stats();
        }

        public string? GetLastError()
        {
            if (!_isAvailable) return "Native library not available";

            try
            {
                IntPtr errorPtr = NexusInterop.nexus_get_last_error();
                if (errorPtr == IntPtr.Zero) return null;

                string error = Marshal.PtrToStringAnsi(errorPtr) ?? string.Empty;
                NexusInterop.nexus_free_string(errorPtr);
                return error;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
