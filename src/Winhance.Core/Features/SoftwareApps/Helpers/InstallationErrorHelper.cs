using System;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Helpers
{
    /// <summary>
    /// Helper class for installation error handling.
    /// </summary>
    public static class InstallationErrorHelper
    {
        /// <summary>
        /// Gets a user-friendly error message based on the error type.
        /// </summary>
        /// <param name="errorType">The type of installation error.</param>
        /// <returns>A user-friendly error message.</returns>
        public static string GetUserFriendlyErrorMessage(InstallationErrorType errorType)
        {
            return errorType switch
            {
                InstallationErrorType.NetworkError =>
                    "Network connection error. Please check your internet connection and try again.",

                InstallationErrorType.PermissionError =>
                    "Permission denied. Please run the application with administrator privileges.",

                InstallationErrorType.PackageNotFoundError =>
                    "Package not found. The requested package may not be available in the repositories.",

                InstallationErrorType.WinGetNotInstalledError =>
                    "WinGet is not installed and could not be installed automatically.",

                InstallationErrorType.AlreadyInstalledError =>
                    "The package is already installed.",

                InstallationErrorType.CancelledByUserError =>
                    "The installation was cancelled by the user.",

                InstallationErrorType.SystemStateError =>
                    "The system is in a state that prevents installation. Please restart your computer and try again.",

                InstallationErrorType.PackageCorruptedError =>
                    "The package is corrupted or invalid. Please try reinstalling or contact the package maintainer.",

                InstallationErrorType.DependencyResolutionError =>
                    "The package dependencies could not be resolved. Some required components may be missing.",

                InstallationErrorType.UnknownError or _ =>
                    "An unknown error occurred during installation. Please check the logs for more details.",
            };
        }

        /// <summary>
        /// Determines the error type based on the exception message.
        /// </summary>
        /// <param name="exceptionMessage">The exception message.</param>
        /// <returns>The determined error type.</returns>
        public static InstallationErrorType DetermineErrorType(string exceptionMessage)
        {
            if (string.IsNullOrEmpty(exceptionMessage))
            {
                return InstallationErrorType.UnknownError;
            }

            exceptionMessage = exceptionMessage.ToLowerInvariant();

            if (exceptionMessage.Contains("network", StringComparison.Ordinal) ||
                exceptionMessage.Contains("connection", StringComparison.Ordinal) ||
                exceptionMessage.Contains("internet", StringComparison.Ordinal) ||
                exceptionMessage.Contains("timeout", StringComparison.Ordinal) ||
                exceptionMessage.Contains("unreachable", StringComparison.Ordinal))
            {
                return InstallationErrorType.NetworkError;
            }

            if (exceptionMessage.Contains("permission", StringComparison.Ordinal) ||
                exceptionMessage.Contains("access", StringComparison.Ordinal) ||
                exceptionMessage.Contains("denied", StringComparison.Ordinal) ||
                exceptionMessage.Contains("administrator", StringComparison.Ordinal) ||
                exceptionMessage.Contains("elevation", StringComparison.Ordinal))
            {
                return InstallationErrorType.PermissionError;
            }

            if (exceptionMessage.Contains("not found", StringComparison.Ordinal) ||
                exceptionMessage.Contains("no package", StringComparison.Ordinal) ||
                exceptionMessage.Contains("no such package", StringComparison.Ordinal) ||
                exceptionMessage.Contains("could not find", StringComparison.Ordinal))
            {
                return InstallationErrorType.PackageNotFoundError;
            }

            if (exceptionMessage.Contains("winget", StringComparison.Ordinal) &&
                (exceptionMessage.Contains("not installed", StringComparison.Ordinal) ||
                 exceptionMessage.Contains("could not be installed", StringComparison.Ordinal)))
            {
                return InstallationErrorType.WinGetNotInstalledError;
            }

            if (exceptionMessage.Contains("already installed", StringComparison.Ordinal) ||
                exceptionMessage.Contains("is installed", StringComparison.Ordinal))
            {
                return InstallationErrorType.AlreadyInstalledError;
            }

            if (exceptionMessage.Contains("cancelled", StringComparison.Ordinal) ||
                exceptionMessage.Contains("canceled", StringComparison.Ordinal) ||
                exceptionMessage.Contains("aborted", StringComparison.Ordinal))
            {
                return InstallationErrorType.CancelledByUserError;
            }

            if (exceptionMessage.Contains("system state", StringComparison.Ordinal) ||
                exceptionMessage.Contains("restart", StringComparison.Ordinal) ||
                exceptionMessage.Contains("reboot", StringComparison.Ordinal))
            {
                return InstallationErrorType.SystemStateError;
            }

            if (exceptionMessage.Contains("corrupt", StringComparison.Ordinal) ||
                exceptionMessage.Contains("invalid", StringComparison.Ordinal) ||
                exceptionMessage.Contains("damaged", StringComparison.Ordinal))
            {
                return InstallationErrorType.PackageCorruptedError;
            }

            if (exceptionMessage.Contains("dependency", StringComparison.Ordinal) ||
                exceptionMessage.Contains("dependencies", StringComparison.Ordinal) ||
                exceptionMessage.Contains("requires", StringComparison.Ordinal))
            {
                return InstallationErrorType.DependencyResolutionError;
            }

            return InstallationErrorType.UnknownError;
        }
    }
}
