using System;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.FileManager.Interop
{
    /// <summary>
    /// P/Invoke bindings for the high-performance Rust nexus_core library.
    /// Provides ultra-fast file indexing using MFT reading and USN Journal monitoring.
    /// Pure performance - direct native calls with minimal marshaling overhead.
    /// </summary>
    public static class NexusInterop
    {
        private const string DllName = "nexus_core";

        // ====================================================================
        // INITIALIZATION
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool nexus_init();

        // ====================================================================
        // INDEXING
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long nexus_index_all();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern long nexus_index_directory([MarshalAs(UnmanagedType.LPStr)] string path);

        // ====================================================================
        // SEARCH
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern long nexus_search([MarshalAs(UnmanagedType.LPStr)] string query, uint maxResults);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr nexus_get_search_result(uint index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void nexus_clear_search_results();

        // ====================================================================
        // PROGRESS TRACKING
        // ====================================================================

        public delegate void ProgressCallback(ulong current, ulong total, IntPtr phase);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void nexus_set_progress_callback(ProgressCallback callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void nexus_clear_progress_callback();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong nexus_get_progress_current();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong nexus_get_progress_total();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool nexus_is_indexing();

        // ====================================================================
        // CONTENT HASHING (for duplicate detection)
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern ulong nexus_hash_file_quick([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr nexus_hash_file_full([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long nexus_find_duplicates(ulong minSize);

        // ====================================================================
        // STATISTICS
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FfiIndexStats nexus_get_stats();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong nexus_get_file_count();

        // ====================================================================
        // MEMORY MANAGEMENT
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void nexus_free_string(IntPtr str);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr nexus_get_last_error();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void nexus_free_result(IntPtr result);
    }

    /// <summary>
    /// FFI search result structure matching Rust FfiSearchResult.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FfiSearchResult
    {
        public IntPtr Path;
        public IntPtr Name;
        public ulong Size;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsDir;

        public string GetPath() => Marshal.PtrToStringAnsi(Path) ?? string.Empty;
        public string GetName() => Marshal.PtrToStringAnsi(Name) ?? string.Empty;
    }

    /// <summary>
    /// FFI index statistics structure matching Rust FfiIndexStats.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FfiIndexStats
    {
        public ulong TotalFiles;
        public ulong TotalDirs;
        public ulong TotalSize;
        public ulong IndexTimeMs;
    }
}
