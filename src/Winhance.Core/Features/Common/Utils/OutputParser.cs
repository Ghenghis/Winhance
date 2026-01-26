using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Utils
{
    public static class OutputParser
    {
        private static readonly Regex GuidRegex = new(
            @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})",
            RegexOptions.Compiled);

        private static readonly Regex ParenthesesContentRegex = new(
            @"\((.+?)\)",
            RegexOptions.Compiled);

        private static readonly Regex HexValueRegex = new(
            @"0x[0-9a-fA-F]+",
            RegexOptions.Compiled);

        public static class PowerCfg
        {
            public static string? ExtractGuid(string output)
                => GuidRegex.Match(output).Success ? GuidRegex.Match(output).Value : null;

            public static string? ExtractNameFromParentheses(string output)
                => ParenthesesContentRegex.Match(output).Success
                    ? ParenthesesContentRegex.Match(output).Groups[1].Value.Trim()
                    : null;

            public static int? ParseAcValue(string output)
            {
                return ParseValueWithHeuristics(output, isAc: true);
            }

            public static int? ParseDcValue(string output)
            {
                return ParseValueWithHeuristics(output, isAc: false);
            }

            private static int? ParseValueWithHeuristics(string output, bool isAc)
            {
                if (string.IsNullOrEmpty(output))
                {
                    return null;
                }

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Use indentation heuristic (4 spaces) + Hex value
                    // And check for AC/DC keywords
                    if (line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("     ", StringComparison.Ordinal) && line.Contains("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        var upper = line.ToUpperInvariant();
                        bool match = false;
                        if (isAc)
                        {
                            match = upper.Contains("AC", StringComparison.Ordinal) || upper.Contains("CA", StringComparison.Ordinal) || upper.Contains("NETZ", StringComparison.Ordinal);
                        }
                        else
                        {
                            match = upper.Contains("DC", StringComparison.Ordinal) || upper.Contains("CC", StringComparison.Ordinal) || upper.Contains("BAT", StringComparison.Ordinal) || upper.Contains("AKKU", StringComparison.Ordinal);
                        }

                        if (match)
                        {
                            var hexMatch = HexValueRegex.Match(line);
                            if (hexMatch.Success)
                            {
                                return ParseIndexValue(hexMatch.Value);
                            }
                        }
                    }
                }

                return null;
            }

            // Deprecated/Legacy method - tries to use pattern but falls back if needed
            public static int? ParsePowerSettingValue(string output, string searchPattern)
            {
                // If search pattern is the standard English one, try our robust parser first
                if (searchPattern.Contains("AC Power Setting Index", StringComparison.Ordinal))
                {
                    return ParseAcValue(output);
                }

                if (searchPattern.Contains("DC Power Setting Index", StringComparison.Ordinal))
                {
                    return ParseDcValue(output);
                }

                if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(searchPattern))
                {
                    return null;
                }

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith(searchPattern, StringComparison.Ordinal))
                        {
                            var valueStart = trimmed.IndexOf(searchPattern, StringComparison.Ordinal) + searchPattern.Length;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            return ParseIndexValue(valueString);
                        }
                    }

                    return null;
                }
                catch (FormatException ex)
                {
                    Debug.WriteLine($"OutputParser.ParsePowerSettingValue format error: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParsePowerSettingValue unexpected error: {ex.Message}");
                    return null;
                }
            }

            public static string? ExtractPowerSchemeGuid(string powercfgOutput)
            {
                try
                {
                    var lines = powercfgOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Check for GUID. Scheme GUID is usually the first one in output or indentation 0
                        if (GuidRegex.IsMatch(line))
                        {
                            return ExtractGuid(line);
                        }
                    }

                    return null;
                }
                catch (FormatException ex)
                {
                    Debug.WriteLine($"OutputParser.ExtractPowerSchemeGuid format error: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ExtractPowerSchemeGuid unexpected error: {ex.Message}");
                    return null;
                }
            }

            public static Dictionary<string, Dictionary<string, int?>> ParseBulkPowerSettingsOutput(string output)
            {
                var results = new Dictionary<string, Dictionary<string, int?>>();
                if (string.IsNullOrEmpty(output))
                {
                    return results;
                }

                try
                {
                    var subgroupSections = ParseSubgroupSections(output);

                    foreach (var (subgroupGuid, subgroupContent) in subgroupSections)
                    {
                        var settingValues = ParseSettingsInSubgroup(subgroupContent);
                        foreach (var (settingGuid, values) in settingValues)
                        {
                            var key = settingGuid;
                            results[key] = values;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParseBulkPowerSettingsOutput error: {ex.Message}");

                    // Return partial results if parsing fails,
                }

                return results;
            }

            public static (List<PowerPlan> powerPlans, Dictionary<string, int?> powerSettings) ParseDelimitedPowerOutput(string output)
            {
                var powerPlans = new List<PowerPlan>();
                var powerSettings = new Dictionary<string, int?>();

                try
                {
                    var planStartIndex = output.IndexOf("=== POWER_PLANS_START ===", StringComparison.Ordinal);
                    var planEndIndex = output.IndexOf("=== POWER_PLANS_END ===", StringComparison.Ordinal);

                    if (planStartIndex != -1 && planEndIndex != -1)
                    {
                        var planSection = output.Substring(
                            planStartIndex + "=== POWER_PLANS_START ===".Length,
                            planEndIndex - planStartIndex - "=== POWER_PLANS_START ===".Length);
                        powerPlans = ParsePowerPlansFromListOutput(planSection.Trim());
                    }

                    var settingsStartIndex = output.IndexOf("=== POWER_SETTINGS_START ===", StringComparison.Ordinal);
                    var settingsEndIndex = output.IndexOf("=== POWER_SETTINGS_END ===", StringComparison.Ordinal);

                    if (settingsStartIndex != -1 && settingsEndIndex != -1)
                    {
                        var settingsSection = output.Substring(
                            settingsStartIndex + "=== POWER_SETTINGS_START ===".Length,
                            settingsEndIndex - settingsStartIndex - "=== POWER_SETTINGS_START ===".Length);
                        var bulkResults = ParseBulkPowerSettingsOutput(settingsSection.Trim());

                        powerSettings = FlattenPowerSettings(bulkResults);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParseDelimitedPowerOutput error: {ex.Message}");

                    // Return partial results if parsing fails,
                }

                return (powerPlans, powerSettings);
            }

            public static List<PowerPlan> ParsePowerPlansFromListOutput(string planOutput)
            {
                var powerPlans = new List<PowerPlan>();
                if (string.IsNullOrEmpty(planOutput))
                {
                    return powerPlans;
                }

                try
                {
                    var lines = planOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Robust check: contains a GUID
                        if (GuidRegex.IsMatch(line))
                        {
                            var guid = ExtractGuid(line);
                            var name = ExtractNameFromParentheses(line);
                            bool isActive = line.Trim().EndsWith("*", StringComparison.Ordinal);

                            if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(name))
                            {
                                powerPlans.Add(new PowerPlan
                                {
                                    Guid = guid,
                                    Name = name,
                                    IsActive = isActive,
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParsePowerPlansFromListOutput error: {ex.Message}");

                    // Return empty list if parsing fails,
                }

                return powerPlans.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
            }

            public static Dictionary<string, int?> FlattenPowerSettings(Dictionary<string, Dictionary<string, int?>> bulkResults)
            {
                var flatResults = new Dictionary<string, int?>();
                foreach (var settingData in bulkResults)
                {
                    var key = settingData.Key;
                    var acDcValues = settingData.Value;
                    var value = acDcValues.TryGetValue("AC", out var acValue) ? acValue :
                               acDcValues.TryGetValue("DC", out var dcValue) ? dcValue : null;
                    flatResults[key] = value;
                }

                return flatResults;
            }

            private static Dictionary<string, string> ParseSubgroupSections(string output)
            {
                var sections = new Dictionary<string, string>();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? currentSubgroupGuid = null;
                var currentContent = new StringBuilder();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Heuristic: Subgroup lines are indented by 2 spaces
                    bool isSubgroupLine = GuidRegex.IsMatch(line) && line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal);

                    if (trimmed.StartsWith("Subgroup GUID:", StringComparison.Ordinal) || isSubgroupLine)
                    {
                        if (currentSubgroupGuid != null)
                        {
                            sections[currentSubgroupGuid] = currentContent.ToString();
                        }

                        currentSubgroupGuid = ExtractGuid(trimmed);
                        currentContent.Clear();
                    }
                    else if (currentSubgroupGuid != null)
                    {
                        currentContent.AppendLine(trimmed); // Note: this strips indentation for content processing,
                    }
                }

                if (currentSubgroupGuid != null)
                {
                    sections[currentSubgroupGuid] = currentContent.ToString();
                }

                return sections;
            }

            public static Dictionary<string, int?> ParseFilteredPowerSettingsOutput(string output)
            {
                var results = new Dictionary<string, int?>();
                if (string.IsNullOrEmpty(output))
                {
                    return results;
                }

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    string? currentSettingGuid = null;
                    int? currentACValue = null;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();

                        // Setting lines indented by 4 spaces
                        bool isSettingLine = GuidRegex.IsMatch(line) && line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("     ", StringComparison.Ordinal);

                        if (trimmed.StartsWith("Power Setting GUID:", StringComparison.Ordinal) || isSettingLine)
                        {
                            // Save previous setting if we have complete data
                            if (currentSettingGuid != null && currentACValue.HasValue)
                            {
                                results[currentSettingGuid] = currentACValue;
                            }

                            // Start new setting
                            currentSettingGuid = ExtractGuid(trimmed);
                            currentACValue = null;
                        }
                        else
                        {
                            // Try parsing values
                            if (line.Contains("0x", StringComparison.Ordinal) && (line.Contains("AC", StringComparison.OrdinalIgnoreCase) || line.Contains("CA", StringComparison.OrdinalIgnoreCase) || line.Contains("NETZ", StringComparison.OrdinalIgnoreCase)))
                            {
                                var hexMatch = HexValueRegex.Match(line);
                                if (hexMatch.Success)
                                {
                                    currentACValue = ParseIndexValue(hexMatch.Value);
                                }
                            }
                            else if (trimmed.StartsWith("Current AC Power Setting Index:", StringComparison.Ordinal))
                            {
                                var colonIndex = trimmed.IndexOf(':');
                                if (colonIndex != -1)
                                {
                                    var valueStr = trimmed.Substring(colonIndex + 1).Trim();
                                    currentACValue = ParseIndexValue(valueStr);
                                }
                            }
                        }
                    }

                    // Don't forget the last setting
                    if (currentSettingGuid != null && currentACValue.HasValue)
                    {
                        results[currentSettingGuid] = currentACValue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParseFilteredPowerSettingsOutput error: {ex.Message}");

                    // Return partial results if parsing fails,
                }

                return results;
            }

            private static Dictionary<string, Dictionary<string, int?>> ParseSettingsInSubgroup(string subgroupContent)
            {
                var settings = new Dictionary<string, Dictionary<string, int?>>();
                var lines = subgroupContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? currentSettingGuid = null;
                var currentValues = new Dictionary<string, int?>();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Note: In ParseSubgroupSections, we used trimmed lines to build content, so indentation might be lost
                    // if it was trimmed before appending.
                    // Let's check ParseSubgroupSections again.
                    // "currentContent.AppendLine(trimmed);" -> Yes, indentation is lost in the content passed here.
                    // So we can't use indentation check here easily if it was stripped!
                    // BUT: The content of this method receives the output of `ParseSubgroupSections` which constructed it.
                    // `ParseSubgroupSections` appends `trimmed`. So indentation is GONE.
                    // We must rely on GuidRegex matching since indentation is 0.
                    bool isSettingLine = GuidRegex.IsMatch(trimmed);

                    if (trimmed.StartsWith("Power Setting GUID:", StringComparison.Ordinal) || isSettingLine)
                    {
                        if (currentSettingGuid != null)
                        {
                            settings[currentSettingGuid] = new Dictionary<string, int?>(currentValues);
                        }

                        currentSettingGuid = ExtractGuid(trimmed);
                        currentValues.Clear();
                    }
                    else
                    {
                        // Parse values (Robust)
                        var upper = trimmed.ToUpperInvariant();
                        if (trimmed.Contains("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            var hexMatch = HexValueRegex.Match(trimmed);
                            if (hexMatch.Success)
                            {
                                if (upper.Contains("AC", StringComparison.Ordinal) || upper.Contains("CA", StringComparison.Ordinal) || upper.Contains("NETZ", StringComparison.Ordinal))
                                {
                                    currentValues["AC"] = ParseIndexValue(hexMatch.Value);
                                }
                                else if (upper.Contains("DC", StringComparison.Ordinal) || upper.Contains("CC", StringComparison.Ordinal) || upper.Contains("BAT", StringComparison.Ordinal) || upper.Contains("AKKU", StringComparison.Ordinal))
                                {
                                    currentValues["DC"] = ParseIndexValue(hexMatch.Value);
                                }
                            }
                        }

                        // Fallback to specific strings if strict match fails but specific strings are present (unlikely if robust failed)
                        if (!currentValues.ContainsKey("AC") && trimmed.StartsWith("Current AC Power Setting Index:", StringComparison.Ordinal))
                        {
                            var colonIndex = trimmed.IndexOf(':');
                            if (colonIndex != -1)
                            {
                                currentValues["AC"] = ParseIndexValue(trimmed.Substring(colonIndex + 1));
                            }
                        }
                        else if (!currentValues.ContainsKey("DC") && trimmed.StartsWith("Current DC Power Setting Index:", StringComparison.Ordinal))
                        {
                            var colonIndex = trimmed.IndexOf(':');
                            if (colonIndex != -1)
                            {
                                currentValues["DC"] = ParseIndexValue(trimmed.Substring(colonIndex + 1));
                            }
                        }
                    }
                }

                if (currentSettingGuid != null)
                {
                    settings[currentSettingGuid] = currentValues;
                }

                return settings;
            }

            public static (int? minValue, int? maxValue) ParsePowerSettingMinMax(string output)
            {
                if (string.IsNullOrEmpty(output))
                {
                    return (null, null);
                }

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    int? minValue = null;
                    int? maxValue = null;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();

                        if (trimmed.StartsWith("Minimum Possible Setting:", StringComparison.Ordinal))
                        {
                            var valueStart = trimmed.IndexOf(':') + 1;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            minValue = ParseIndexValue(valueString);
                        }
                        else if (trimmed.StartsWith("Maximum Possible Setting:", StringComparison.Ordinal))
                        {
                            var valueStart = trimmed.IndexOf(':') + 1;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            maxValue = ParseIndexValue(valueString);
                        }
                    }

                    return (minValue, maxValue);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OutputParser.ParsePowerSettingMinMax error: {ex.Message}");
                    return (null, null);
                }
            }

            private static int? ParseIndexValue(string valueString)
            {
                if (string.IsNullOrEmpty(valueString))
                {
                    return null;
                }

                var parts = valueString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(part.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                        {
                            return hexValue;
                        }
                    }
                    else if (int.TryParse(part, out int decValue))
                    {
                        return decValue;
                    }
                }

                return null;
            }
        }
    }
}
