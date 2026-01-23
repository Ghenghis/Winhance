using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsRegistryService(ILogService logService) : IWindowsRegistryService
    {
        /// <summary>
        /// Sensitive registry paths that should not be modified by this service.
        /// These are critical system paths that could compromise security if modified.
        /// </summary>
        private static readonly HashSet<string> SensitiveRegistryPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication",
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders",
            @"HKEY_LOCAL_MACHINE\SAM",
            @"HKEY_LOCAL_MACHINE\SECURITY",
        };

        /// <summary>
        /// Validates a registry key path for safety.
        /// Prevents injection attacks and access to sensitive system keys.
        /// </summary>
        private static void ValidateRegistryPath(string keyPath, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(keyPath, parameterName);

            if (string.IsNullOrWhiteSpace(keyPath))
            {
                throw new ArgumentException("Registry path cannot be empty", parameterName);
            }

            // Check for invalid characters that could be used for injection
            var invalidChars = new[] { '\0', '\r', '\n', '\t' };
            if (keyPath.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException($"Registry path contains invalid characters: {parameterName}", parameterName);
            }

            // Check for path traversal patterns
            if (keyPath.Contains(".."))
            {
                throw new ArgumentException("Registry path traversal not allowed", parameterName);
            }

            // Block access to sensitive registry paths
            foreach (var sensitivePath in SensitiveRegistryPaths)
            {
                if (keyPath.StartsWith(sensitivePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Access to sensitive registry path is not allowed: {sensitivePath}");
                }
            }
        }

        /// <summary>
        /// Validates a registry value name for safety.
        /// </summary>
        private static void ValidateValueName(string? valueName)
        {
            if (valueName == null)
                return;

            var invalidChars = new[] { '\0', '\r', '\n' };
            if (valueName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException("Value name contains invalid characters", nameof(valueName));
            }
        }

        public bool CreateKey(string keyPath)
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));

                if (KeyExists(keyPath))
                    return true;

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var createdKey = rootKey.CreateSubKey(subKeyPath, true);
                return createdKey != null;
            }
            catch (UnauthorizedAccessException ex)
            {
                logService.Log(LogLevel.Warning, $"Access denied to registry key '{keyPath}': {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                logService.Log(LogLevel.Warning, $"Invalid registry key path '{keyPath}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to create registry key '{keyPath}': {ex.Message}");
                return false;
            }
        }

        public bool SetValue(
            string keyPath,
            string valueName,
            object value,
            RegistryValueKind valueKind
        )
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));
                ValidateValueName(valueName);

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var targetKey = rootKey.CreateSubKey(subKeyPath, true);
                if (targetKey == null)
                    return false;

                targetKey.SetValue(valueName, value, valueKind);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                logService.Log(LogLevel.Warning, $"Access denied to registry value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                logService.Log(LogLevel.Warning, $"Invalid registry path or value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to set registry value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public object? GetValue(string keyPath, string valueName)
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));
                ValidateValueName(valueName);

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                return key?.GetValue(valueName);
            }
            catch (UnauthorizedAccessException ex)
            {
                logService.Log(LogLevel.Warning, $"Access denied to registry value '{keyPath}\\{valueName}': {ex.Message}");
                return null;
            }
            catch (ArgumentException ex)
            {
                logService.Log(LogLevel.Warning, $"Invalid registry path or value '{keyPath}\\{valueName}': {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to get registry value '{keyPath}\\{valueName}': {ex.Message}");
                return null;
            }
        }

        public bool DeleteKey(string keyPath)
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));

                if (!KeyExists(keyPath))
                    return true;

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                rootKey.DeleteSubKeyTree(subKeyPath, false);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                logService.Log(LogLevel.Warning, $"Access denied to delete registry key '{keyPath}': {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                logService.Log(LogLevel.Warning, $"Invalid registry key path '{keyPath}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to delete registry key '{keyPath}': {ex.Message}");
                return false;
            }
        }

        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));
                ValidateValueName(valueName);

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, true);
                if (key == null)
                    return false;

                key.DeleteValue(valueName, false);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                logService.Log(LogLevel.Warning, $"Access denied to delete registry value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                logService.Log(LogLevel.Warning, $"Invalid registry path or value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to delete registry value '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public bool KeyExists(string keyPath)
        {
            try
            {
                ValidateRegistryPath(keyPath, nameof(keyPath));

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                return key != null;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Failed to check if registry key exists '{keyPath}': {ex.Message}");
                return false;
            }
        }

        public bool ValueExists(string keyPath, string valueName)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                if (key == null)
                    return false;

                return key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Failed to check if registry value exists '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public bool RegistryValueExists(RegistrySetting setting)
        {
            try
            {
                if (setting?.ValueName == null)
                    return KeyExists(setting?.KeyPath ?? "");

                if (!KeyExists(setting.KeyPath))
                    return false;

                return ValueExists(setting.KeyPath, setting.ValueName);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error checking registry value existence for {setting?.KeyPath}\\{setting?.ValueName}: {ex.Message}");
                return false;
            }
        }

        public bool IsSettingApplied(RegistrySetting setting)
        {
            try
            {
                if (setting == null)
                    return false;

                // For settings that check the (Default) value with ValueName = null,
                // we need to check if both EnabledValue and DisabledValue are null.
                // If both are null, we're just checking key existence.
                // If either has a value, we need to read and compare the default value.
                if (setting.ValueName == null && setting.EnabledValue == null && setting.DisabledValue == null)
                {
                    return KeyExists(setting.KeyPath);
                }

                if (!KeyExists(setting.KeyPath))
                {
                    return setting.AbsenceMeansEnabled;
                }

                if (!ValueExists(setting.KeyPath, setting.ValueName))
                {
                    return setting.AbsenceMeansEnabled;
                }

                if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
                {
                    return IsBitSet(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, setting.BitMask.Value);
                }

                if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
                {
                    var currentByte = GetBinaryByte(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value);
                    if (currentByte == null)
                        return false;

                    var enabledByte = setting.EnabledValue switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ => (byte)0
                    };

                    return currentByte.Value == enabledByte;
                }

                var currentValue = GetValue(setting.KeyPath, setting.ValueName);

                // Check if current value matches EnabledValue (only if EnabledValue is not null)
                if (setting.EnabledValue != null && CompareValues(currentValue, setting.EnabledValue))
                    return true;

                // Check if current value matches DisabledValue (only if DisabledValue is not null)
                if (setting.DisabledValue != null && CompareValues(currentValue, setting.DisabledValue))
                    return false;

                // Value doesn't match either EnabledValue or DisabledValue
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Failed to check if setting is applied '{setting?.KeyPath}': {ex.Message}");
                return false;
            }
        }

        public bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is not byte[] currentBytes)
                {
                    var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                    defaultBinary[byteIndex] = newValue;
                    return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
                }

                if (currentBytes.Length <= byteIndex)
                {
                    var expandedBytes = new byte[byteIndex + 1];
                    Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                    expandedBytes[byteIndex] = newValue;
                    return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
                }

                var modifiedBytes = (byte[])currentBytes.Clone();
                modifiedBytes[byteIndex] = newValue;

                return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public byte? GetBinaryByte(string keyPath, string valueName, int byteIndex)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is byte[] currentBytes && currentBytes.Length > byteIndex)
                {
                    return currentBytes[byteIndex];
                }
                return null;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Failed to get binary byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return null;
            }
        }

        public bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is not byte[] currentBytes)
                {
                    var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                    defaultBinary[byteIndex] = setBit ? bitMask : (byte)0;
                    return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
                }

                if (currentBytes.Length <= byteIndex)
                {
                    var expandedBytes = new byte[byteIndex + 1];
                    Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                    expandedBytes[byteIndex] = setBit ? bitMask : (byte)0;
                    return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
                }

                var modifiedBytes = (byte[])currentBytes.Clone();
                if (setBit)
                    modifiedBytes[byteIndex] |= bitMask;
                else
                    modifiedBytes[byteIndex] &= (byte)~bitMask;

                return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying bit mask 0x{bitMask:X2} at byte index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public bool IsBitSet(string keyPath, string valueName, int byteIndex, byte bitMask)
        {
            try
            {
                var currentByte = GetBinaryByte(keyPath, valueName, byteIndex);
                if (!currentByte.HasValue)
                    return false;

                return (currentByte.Value & bitMask) == bitMask;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Failed to check if bit is set at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        public bool ApplySetting(RegistrySetting setting, bool isEnabled, object? specificValue = null)
        {
            if (setting == null)
                return false;

            try
            {
                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Applying registry setting - Path: {setting.KeyPath}, Value: {setting.ValueName}, Enabled: {isEnabled}");

                if (setting.ValueName == null)
                {
                    var result = isEnabled ? CreateKey(setting.KeyPath) : DeleteKey(setting.KeyPath);
                    return result;
                }

                if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
                {
                    if (!CreateKey(setting.KeyPath))
                        return false;

                    var result = ModifyBinaryBit(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, setting.BitMask.Value, isEnabled);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified bit mask 0x{setting.BitMask.Value:X2} at byte index {setting.BinaryByteIndex.Value} to {isEnabled} - Success: {result}");
                    return result;
                }

                if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
                {
                    var byteValue = specificValue switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ when isEnabled => setting.EnabledValue switch
                        {
                            byte b => b,
                            int i => (byte)i,
                            _ => (byte)0
                        },
                        _ => setting.DisabledValue switch
                        {
                            byte b => b,
                            int i => (byte)i,
                            _ => (byte)0
                        }
                    };

                    if (!CreateKey(setting.KeyPath))
                        return false;

                    var result = ModifyBinaryByte(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, byteValue);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified byte at index {setting.BinaryByteIndex.Value} to {byteValue:X2} - Success: {result}");
                    return result;
                }

                var oldValue = GetValue(setting.KeyPath, setting.ValueName);
                var valueToSet = specificValue ?? (isEnabled
                    ? setting.EnabledValue
                    : setting.DisabledValue);

                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Setting '{setting.KeyPath}\\{setting.ValueName}' - Old: {oldValue}, New: {valueToSet}");

                if (valueToSet == null)
                {
                    var result = DeleteValue(setting.KeyPath, setting.ValueName);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Deleted value '{setting.ValueName}' from '{setting.KeyPath}' - Success: {result}");
                    return result;
                }

                if (!CreateKey(setting.KeyPath))
                    return false;

                var setResult = SetValue(setting.KeyPath, setting.ValueName, valueToSet, setting.ValueType);

                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Set value '{setting.ValueName}' = '{valueToSet}' in '{setting.KeyPath}' - Success: {setResult}");
                return setResult;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error applying setting '{setting.KeyPath}\\{setting.ValueName}': {ex.Message}");
                return false;
            }
        }

        private static (RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
        {
            var parts = keyPath.Split('\\', 2);
            if (parts.Length < 2)
                throw new ArgumentException($"Invalid registry key path: {keyPath}");

            var rootKey = parts[0].ToUpperInvariant() switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
                "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
                "HKEY_USERS" or "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
                _ => throw new ArgumentException($"Invalid registry hive: {parts[0]}"),
            };

            return (rootKey, parts[1]);
        }

        public Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string valueName)> queries)
        {
            var results = new Dictionary<string, object?>();
            var queriesByHive = queries.GroupBy(q => GetHiveFromPath(q.keyPath));
            
            foreach (var hiveGroup in queriesByHive)
            {
                var rootKey = hiveGroup.Key;
                
                foreach (var (keyPath, valueName) in hiveGroup)
                {
                    try
                    {
                        var (_, subKeyPath) = ParseKeyPath(keyPath);
                        using var subKey = rootKey.OpenSubKey(subKeyPath, false);

                        var resultKey = valueName == null
                            ? $"{keyPath}\\__KEY_EXISTS__"
                            : $"{keyPath}\\{valueName}";

                        if (valueName == null)
                        {
                            results[resultKey] = subKey != null;
                        }
                        else
                        {
                            results[resultKey] = subKey?.GetValue(valueName);
                        }
                    }
                    catch (Exception)
                    {
                        var resultKey = valueName == null
                            ? $"{keyPath}\\__KEY_EXISTS__"
                            : $"{keyPath}\\{valueName}";
                        results[resultKey] = null;
                    }
                }
            }
            
            return results;
        }

        private static RegistryKey GetHiveFromPath(string keyPath)
        {
            var parts = keyPath.Split('\\', 2);
            return parts[0].ToUpperInvariant() switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
                "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
                "HKEY_USERS" or "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
                _ => Registry.CurrentUser,
            };
        }

        private static bool CompareValues(object? current, object? desired)
        {
            return current switch
            {
                null => desired == null,
                int i when desired is int d => i == d,
                string s when desired is string ds => s.Equals(
                    ds,
                    StringComparison.OrdinalIgnoreCase
                ),
                byte[] ba when desired is byte[] dba => ba.SequenceEqual(dba),
                _ => current.Equals(desired),
            };
        }
    }
}
