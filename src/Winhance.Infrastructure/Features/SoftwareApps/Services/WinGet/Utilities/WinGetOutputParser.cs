using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    public class WinGetOutputParser
    {
        private string _lastLine = string.Empty;
        private readonly string _appName;

        public WinGetOutputParser(string appName = null!)
        {
            _appName = appName;
        }

        public InstallationProgress ParseOutputLine(string outputLine)
        {
            if (string.IsNullOrWhiteSpace(outputLine))
            {
                return null;
            }

            string trimmedLine = outputLine.Trim();

            // Check for network-related errors first
            if (IsNetworkRelatedError(trimmedLine))
            {
                return new InstallationProgress
                {
                    Status = $"Network issue detected while processing {_appName}",
                    LastLine = trimmedLine,
                    IsActive = true,
                    IsError = true,
                    IsConnectivityIssue = true,
                };
            }

            // If line contains progress bar characters, don't show the raw output
            if (ContainsProgressBar(trimmedLine))
            {
                _lastLine = string.Empty; // Hide the corrupted progress bar,
            }
            else
            {
                _lastLine = trimmedLine;
            }

            // Check for completion
            if (outputLine.Contains("Successfully installed", StringComparison.Ordinal) ||
                outputLine.Contains("Successfully uninstalled", StringComparison.Ordinal) ||
                outputLine.Contains("completed successfully", StringComparison.Ordinal) ||
                outputLine.Contains("installation complete", StringComparison.Ordinal) ||
                outputLine.Contains("uninstallation complete", StringComparison.Ordinal))
            {
                return new InstallationProgress
                {
                    Status = outputLine.Contains("uninstall", StringComparison.Ordinal) ? "Uninstallation completed successfully!" : "Installation completed successfully!",
                    LastLine = _lastLine,
                    IsActive = false,
                };
            }

            // Check if this is an uninstall operation
            bool isUninstall = outputLine.Contains("uninstall", StringComparison.Ordinal) || outputLine.Contains("Uninstall", StringComparison.Ordinal) ||
                              _lastLine.Contains("uninstall", StringComparison.Ordinal) || _lastLine.Contains("Uninstall", StringComparison.Ordinal);

            return new InstallationProgress
            {
                Status = GetStatusMessage(isUninstall),
                LastLine = _lastLine,
                IsActive = true,
            };
        }

        private string GetStatusMessage(bool isUninstall)
        {
            if (string.IsNullOrEmpty(_appName))
            {
                return isUninstall ? "Uninstalling..." : "Installing...";
            }

            // Check for specific installation phases in the last line
            if (!string.IsNullOrEmpty(_lastLine))
            {
                var lowerLine = _lastLine.ToLowerInvariant();

                if (lowerLine.Contains("downloading", StringComparison.Ordinal) || lowerLine.Contains("download", StringComparison.Ordinal))
                {
                    return $"Downloading {_appName}...";
                }

                if (lowerLine.Contains("extracting", StringComparison.Ordinal) || lowerLine.Contains("extract", StringComparison.Ordinal))
                {
                    return $"Extracting {_appName}...";
                }

                if (lowerLine.Contains("installing", StringComparison.Ordinal) || lowerLine.Contains("install", StringComparison.Ordinal))
                {
                    return $"Installing {_appName}...";
                }

                if (lowerLine.Contains("configuring", StringComparison.Ordinal) || lowerLine.Contains("configure", StringComparison.Ordinal))
                {
                    return $"Configuring {_appName}...";
                }

                if (lowerLine.Contains("verifying", StringComparison.Ordinal) || lowerLine.Contains("verify", StringComparison.Ordinal))
                {
                    return $"Verifying {_appName} installation...";
                }

                if (lowerLine.Contains("finalizing", StringComparison.Ordinal) || lowerLine.Contains("finalize", StringComparison.Ordinal))
                {
                    return $"Finalizing {_appName} installation...";
                }
            }

            return isUninstall ? $"Uninstalling {_appName}..." : $"Installing {_appName}...";
        }

        private bool ContainsProgressBar(string line)
        {
            // Check if line contains the corrupted progress bar characters or percentage
            return line.Contains("â–", StringComparison.Ordinal) || // Contains any corrupted block character
                   (line.Contains("%", StringComparison.Ordinal) && line.Length > 10); // Contains percentage and looks like progress,
        }

        private bool IsNetworkRelatedError(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            var lowerLine = line.ToLowerInvariant();
            return lowerLine.Contains("network", StringComparison.Ordinal) ||
                   lowerLine.Contains("timeout", StringComparison.Ordinal) ||
                   lowerLine.Contains("connection", StringComparison.Ordinal) ||
                   lowerLine.Contains("dns", StringComparison.Ordinal) ||
                   lowerLine.Contains("resolve", StringComparison.Ordinal) ||
                   lowerLine.Contains("unreachable", StringComparison.Ordinal) ||
                   lowerLine.Contains("offline", StringComparison.Ordinal) ||
                   lowerLine.Contains("proxy", StringComparison.Ordinal) ||
                   lowerLine.Contains("certificate", StringComparison.Ordinal) ||
                   lowerLine.Contains("ssl", StringComparison.Ordinal) ||
                   lowerLine.Contains("tls", StringComparison.Ordinal) ||
                   lowerLine.Contains("download failed", StringComparison.Ordinal) ||
                   lowerLine.Contains("no internet", StringComparison.Ordinal) ||
                   lowerLine.Contains("connectivity", StringComparison.Ordinal) ||
                   lowerLine.Contains("could not download", StringComparison.Ordinal) ||
                   lowerLine.Contains("failed to download", StringComparison.Ordinal) ||
                   lowerLine.Contains("unable to connect", StringComparison.Ordinal) ||
                   lowerLine.Contains("connection refused", StringComparison.Ordinal) ||
                   lowerLine.Contains("host not found", StringComparison.Ordinal) ||
                   lowerLine.Contains("name resolution failed", StringComparison.Ordinal);
        }

        public void Clear()
        {
            _lastLine = string.Empty;
        }
    }
}
