using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppStatusDiscoveryService(
    ILogService logService,
    IPowerShellExecutionService powerShellExecutionService,
    IWinGetService winGetService) : IAppStatusDiscoveryService
{
    public async Task<Dictionary<string, bool>> GetInstallationStatusBatchAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions.ToList();

        if (definitionList.Count == 0)
        {
            return result;
        }

        try
        {
            var apps = definitionList.Where(d => !string.IsNullOrEmpty(d.AppxPackageName)).ToList();
            var capabilities = definitionList.Where(d => !string.IsNullOrEmpty(d.CapabilityName)).ToList();
            var features = definitionList.Where(d => !string.IsNullOrEmpty(d.OptionalFeatureName)).ToList();

            if (capabilities.Count != 0)
            {
                var capabilityNames = capabilities.Select(c => c.CapabilityName).ToList();
                var capabilityResults = await CheckCapabilitiesAsync(capabilityNames);
                foreach (var capability in capabilities)
                {
                    if (capabilityResults.TryGetValue(capability.CapabilityName, out bool isInstalled))
                    {
                        result[capability.Id] = isInstalled;
                    }
                }
            }

            if (features.Count != 0)
            {
                var featureNames = features.Select(f => f.OptionalFeatureName).ToList();
                var featureResults = await CheckFeaturesAsync(featureNames);
                foreach (var feature in features)
                {
                    if (featureResults.TryGetValue(feature.OptionalFeatureName, out bool isInstalled))
                    {
                        result[feature.Id] = isInstalled;
                    }
                }
            }

            if (apps.Count != 0)
            {
                var installedApps = await GetInstalledStoreAppsAsync();
                foreach (var app in apps)
                {
                    result[app.Id] = installedApps.Contains(app.AppxPackageName);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking batch installation status", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<Dictionary<string, bool>> GetInstallationStatusByIdAsync(IEnumerable<string> appIds)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var appIdList = appIds.ToList();

        if (appIdList.Count == 0)
        {
            return result;
        }

        try
        {
            var installedApps = await GetInstalledStoreAppsAsync();
            foreach (var appId in appIdList)
            {
                result[appId] = installedApps.Contains(appId);
            }

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking installation status by ID", ex);
            return appIdList.ToDictionary(id => id, id => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, bool>> CheckCapabilitiesAsync(List<string> capabilities)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var script = "Get-WindowsCapability -Online | Where-Object State -eq 'Installed' | Select-Object -ExpandProperty Name";
            var scriptOutput = await powerShellExecutionService.ExecuteScriptAsync(script);

            if (!string.IsNullOrEmpty(scriptOutput))
            {
                var installedCapabilities = scriptOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var capability in capabilities)
                {
                    result[capability] = installedCapabilities.Any(c =>
                        c.StartsWith(capability, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                foreach (var capability in capabilities)
                {
                    result[capability] = false;
                }
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking capabilities status", ex);
            foreach (var capability in capabilities)
            {
                result[capability] = false;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, bool>> CheckFeaturesAsync(List<string> features)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var script = "Get-WindowsOptionalFeature -Online | Where-Object State -eq 'Enabled' | Select-Object -ExpandProperty FeatureName";
            var scriptOutput = await powerShellExecutionService.ExecuteScriptAsync(script);

            if (!string.IsNullOrEmpty(scriptOutput))
            {
                var enabledFeatures = scriptOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var feature in features)
                {
                    result[feature] = enabledFeatures.Contains(feature);
                }
            }
            else
            {
                foreach (var feature in features)
                {
                    result[feature] = false;
                }
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking features status", ex);
            foreach (var feature in features)
            {
                result[feature] = false;
            }
        }

        return result;
    }

    private async Task<HashSet<string>> GetInstalledStoreAppsAsync()
    {
        var installedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_InstalledStoreProgram");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        installedApps.Add(name);
                    }
                }
            });

            try
            {
                var registryKeys = new[]
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                };

                foreach (var uninstallKey in registryKeys)
                {
                    if (uninstallKey == null)
                    {
                        continue;
                    }

                    using (uninstallKey)
                    {
                        var subKeyNames = uninstallKey.GetSubKeyNames();

                        if (subKeyNames.Any(name => name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps.Add("Microsoft.Office.OneNote");
                        }

                        if (subKeyNames.Any(name => name.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps.Add("Microsoft.OneDriveSync");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logService.LogError("Error checking registry for apps", ex);
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error querying installed apps via WMI", ex);
        }

        return installedApps;
    }

    public async Task<Dictionary<string, bool>> GetExternalAppsInstallationStatusAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.WinGetPackageId))
            .ToList();

        if (definitionList.Count == 0)
        {
            return result;
        }

        try
        {
            var remainingToCheck = new List<ItemDefinition>(definitionList);
            var foundByWinGetId = 0;
            var foundByWinGetName = 0;

            bool winGetReady = false;
            try
            {
                winGetReady = await winGetService.EnsureWinGetReadyAsync();

                if (!winGetReady)
                {
                    logService.LogInformation("WinGet is not available - skipping WinGet detection, using WMI/Registry only");
                }
            }
            catch (Exception ex)
            {
                logService.LogWarning($"WinGet readiness check failed: {ex.Message}");
                winGetReady = false;
            }

            if (winGetReady)
            {
                var (wingetPackageIds, wingetPackageNames) = await GetAllInstalledWinGetPackagesAsync();

                foreach (var def in definitionList.ToList())
                {
                    if (wingetPackageIds.Contains(def.WinGetPackageId))
                    {
                        result[def.Id] = true;
                        remainingToCheck.Remove(def);
                        foundByWinGetId++;
                    }
                    else if (MatchWinGetName(def.Name, wingetPackageNames))
                    {
                        result[def.Id] = true;
                        remainingToCheck.Remove(def);
                        foundByWinGetName++;
                        logService.LogInformation($"WinGet name match: {def.Name} (Id: {def.Id})");
                    }
                }

                logService.LogInformation($"WinGet: Found {foundByWinGetId} by package ID, {foundByWinGetName} by name ({foundByWinGetId + foundByWinGetName}/{definitionList.Count} total)");
            }

            if (remainingToCheck.Count != 0)
            {
                var wmiTask = GetInstalledProgramsFromWmiOnlyAsync();
                var registryTask = GetInstalledProgramsFromRegistryAsync();

                await Task.WhenAll(wmiTask, registryTask);

                // Use await to get results instead of .Result to avoid blocking
                var wmiPrograms = await wmiTask;
                var registryPrograms = await registryTask;

                var foundByWmi = 0;
                var foundByRegistry = 0;

                foreach (var def in remainingToCheck.ToList())
                {
                    var wmiMatch = FuzzyMatchProgram(def.WinGetPackageId, wmiPrograms);
                    var registryMatch = FuzzyMatchProgram(def.WinGetPackageId, registryPrograms);

                    if (wmiMatch || registryMatch)
                    {
                        result[def.Id] = true;
                        remainingToCheck.Remove(def);
                        if (wmiMatch)
                        {
                            foundByWmi++;
                        }

                        if (registryMatch)
                        {
                            foundByRegistry++;
                        }
                    }
                }

                logService.LogInformation($"Fallback detection: Found {foundByWmi} via WMI, {foundByRegistry} via Registry");
            }

            foreach (var def in remainingToCheck)
            {
                result[def.Id] = false;
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Total: {totalFound}/{definitionList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking batch installed apps: {ex.Message}", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<(HashSet<string> PackageIds, HashSet<string> PackageNames)> GetAllInstalledWinGetPackagesAsync()
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installedPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(async () =>
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "list --accept-source-agreements --disable-interactivity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                var output = new System.Text.StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var processTask = process.WaitForExitAsync();
                var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch (Exception)
                    {
                    }

                    logService.LogWarning("WinGet list command timed out after 30 seconds");
                    return;
                }

                var outputString = output.ToString();

                if (outputString.TrimStart().StartsWith("{", StringComparison.Ordinal))
                {
                    ParseWinGetJsonOutput(outputString, installedPackageIds, installedPackageNames);
                }
                else
                {
                    ParseWinGetTableOutput(outputString, installedPackageIds, installedPackageNames);
                }

                logService.LogInformation($"WinGet returned {installedPackageIds.Count} unique package IDs and {installedPackageNames.Count} unique names");
            });
        }
        catch (Exception ex)
        {
            logService.LogError($"Error getting installed WinGet packages: {ex.Message}", ex);
        }

        return (installedPackageIds, installedPackageNames);
    }

    private void ParseWinGetJsonOutput(string jsonOutput, HashSet<string> installedPackageIds, HashSet<string> installedPackageNames)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(jsonOutput);
            var root = document.RootElement;

            if (root.TryGetProperty("Sources", out var sources))
            {
                foreach (var source in sources.EnumerateArray())
                {
                    if (source.TryGetProperty("Packages", out var packages))
                    {
                        foreach (var package in packages.EnumerateArray())
                        {
                            if (package.TryGetProperty("Id", out var id))
                            {
                                var packageId = id.GetString();
                                if (!string.IsNullOrEmpty(packageId))
                                {
                                    installedPackageIds.Add(packageId);
                                }
                            }

                            if (package.TryGetProperty("Name", out var name))
                            {
                                var packageName = name.GetString();
                                if (!string.IsNullOrEmpty(packageName))
                                {
                                    installedPackageNames.Add(packageName);
                                }
                            }
                        }
                    }
                }
            }

            logService.LogInformation("Parsed WinGet JSON output successfully");
        }
        catch (Exception ex)
        {
            logService.LogError($"Error parsing WinGet JSON output: {ex.Message}", ex);
        }
    }

    private void ParseWinGetTableOutput(string output, HashSet<string> installedPackageIds, HashSet<string> installedPackageNames)
    {
        try
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool headerPassed = false;
            int? nameColumnStart = null;
            int? idColumnStart = null;
            int? versionColumnStart = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.Contains("---", StringComparison.Ordinal))
                {
                    headerPassed = true;

                    if (i > 0)
                    {
                        var headerLine = lines[i - 1];
                        nameColumnStart = headerLine.IndexOf("Name");
                        idColumnStart = headerLine.IndexOf("Id");
                        versionColumnStart = headerLine.IndexOf("Version");
                    }

                    continue;
                }

                if (!headerPassed)
                {
                    continue;
                }

                if (nameColumnStart.HasValue && idColumnStart.HasValue && line.Length > nameColumnStart.Value)
                {
                    var nameSection = line.Substring(nameColumnStart.Value, idColumnStart.Value - nameColumnStart.Value);
                    var packageName = nameSection.Trim();
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        installedPackageNames.Add(packageName);
                    }
                }

                if (idColumnStart.HasValue && line.Length > idColumnStart.Value)
                {
                    var idSection = versionColumnStart.HasValue && line.Length > versionColumnStart.Value
                        ? line.Substring(idColumnStart.Value, versionColumnStart.Value - idColumnStart.Value)
                        : line.Substring(idColumnStart.Value);

                    var packageId = idSection.Trim();

                    if (!string.IsNullOrEmpty(packageId) && packageId.Contains('.'))
                    {
                        installedPackageIds.Add(packageId);
                    }
                }
            }

            logService.LogInformation($"Parsed WinGet table output: {installedPackageIds.Count} IDs, {installedPackageNames.Count} names");
        }
        catch (Exception ex)
        {
            logService.LogError($"Error parsing WinGet table output: {ex.Message}", ex);
        }
    }

    private async Task<HashSet<(string DisplayName, string Publisher)>> GetInstalledProgramsFromWmiOnlyAsync()
    {
        var installedPrograms = new HashSet<(string, string)>();

        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Vendor FROM Win32_InstalledWin32Program");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var name = obj["Name"]?.ToString();
                    var vendor = obj["Vendor"]?.ToString();

                    if (!string.IsNullOrEmpty(name))
                    {
                        installedPrograms.Add((name, vendor ?? string.Empty));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logService.LogError($"Error querying WMI for installed programs: {ex.Message}", ex);
        }

        return installedPrograms;
    }

    private async Task<HashSet<(string DisplayName, string Publisher)>> GetInstalledProgramsFromRegistryAsync()
    {
        var installedPrograms = new HashSet<(string, string)>();

        try
        {
            await Task.Run(() => QueryRegistryForInstalledPrograms(installedPrograms));
        }
        catch (Exception ex)
        {
            logService.LogError($"Error querying registry for installed programs: {ex.Message}", ex);
        }

        return installedPrograms;
    }

    private void QueryRegistryForInstalledPrograms(HashSet<(string, string)> installedPrograms)
    {
        var registryPaths = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (hive, path) in registryPaths)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null)
                {
                    continue;
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null)
                        {
                            continue;
                        }

                        var systemComponent = subKey.GetValue("SystemComponent");
                        if (systemComponent is int systemComponentValue && systemComponentValue == 1)
                        {
                            continue;
                        }

                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        var publisher = subKey.GetValue("Publisher")?.ToString();

                        if (!string.IsNullOrEmpty(displayName))
                        {
                            installedPrograms.Add((displayName, publisher ?? string.Empty));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private bool MatchWinGetName(string definitionName, HashSet<string> wingetPackageNames)
    {
        var normalizedDefName = NormalizeString(definitionName);

        foreach (var wingetName in wingetPackageNames)
        {
            var normalizedWingetName = NormalizeString(wingetName);

            if (normalizedWingetName == normalizedDefName)
            {
                return true;
            }

            if (normalizedWingetName.StartsWith(normalizedDefName + " ", StringComparison.Ordinal) ||
                normalizedWingetName.StartsWith(normalizedDefName + "-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool FuzzyMatchProgram(string winGetPackageId, HashSet<(string DisplayName, string Publisher)> installedPrograms)
    {
        var parts = winGetPackageId.Split('.');

        if (parts.Length < 2)
        {
            var normalized = NormalizeString(winGetPackageId);
            return installedPrograms.Any(p => NormalizeString(p.DisplayName).Contains(normalized));
        }

        var publisher = NormalizeString(parts[0]);
        var productName = NormalizeString(string.Join(".", parts.Skip(1)));

        foreach (var (displayName, vendor) in installedPrograms)
        {
            var normDisplayName = NormalizeString(displayName);
            var normVendor = NormalizeString(vendor);

            if (normDisplayName.Contains(productName))
            {
                if (normDisplayName.Contains("add-in", StringComparison.Ordinal) ||
                    normDisplayName.Contains("for " + productName, StringComparison.Ordinal) ||
                    normDisplayName.Contains("plugin", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(vendor) || normVendor.Contains(publisher))
                {
                    return true;
                }
            }

            var fullId = NormalizeString(winGetPackageId).Replace(".", string.Empty, StringComparison.Ordinal);
            if (normDisplayName.Replace(" ", string.Empty, StringComparison.Ordinal).Contains(fullId))
            {
                return true;
            }
        }

        return false;
    }

    private string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var normalized = input.ToLowerInvariant();

        normalized = normalized
            .Replace("á", "a", StringComparison.Ordinal).Replace("à", "a", StringComparison.Ordinal).Replace("ä", "a", StringComparison.Ordinal).Replace("â", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal).Replace("è", "e", StringComparison.Ordinal).Replace("ë", "e", StringComparison.Ordinal).Replace("ê", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal).Replace("ì", "i", StringComparison.Ordinal).Replace("ï", "i", StringComparison.Ordinal).Replace("î", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal).Replace("ò", "o", StringComparison.Ordinal).Replace("ö", "o", StringComparison.Ordinal).Replace("ô", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal).Replace("ù", "u", StringComparison.Ordinal).Replace("ü", "u", StringComparison.Ordinal).Replace("û", "u", StringComparison.Ordinal)
            .Replace("ñ", "n", StringComparison.Ordinal).Replace("ç", "c", StringComparison.Ordinal);

        return normalized;
    }

    public async Task<Dictionary<string, bool>> CheckInstalledByDisplayNameAsync(IEnumerable<string> displayNames)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var nameList = displayNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        if (nameList.Count == 0)
        {
            return result;
        }

        try
        {
            var registryPrograms = await GetInstalledProgramsFromRegistryAsync();

            foreach (var displayName in nameList)
            {
                var isInstalled = FuzzyMatchProgram(displayName, registryPrograms);
                result[displayName] = isInstalled;
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Display name detection: Found {totalFound}/{nameList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking apps by display name: {ex.Message}", ex);
            return nameList.ToDictionary(name => name, name => false, StringComparer.OrdinalIgnoreCase);
        }
    }
}
