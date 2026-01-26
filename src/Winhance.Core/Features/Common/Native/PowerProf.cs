using System;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native
{
    public static class PowerProf
    {
        // P/Invoke definitions for PowrProf.dll
        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr rootPowerKey,
            IntPtr schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            uint accessFlags,
            uint index,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            uint accessFlags,
            uint index,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            ref Guid subGroupOfPowerSettingsGuid,
            uint accessFlags,
            uint index,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerReadFriendlyName(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadACValueIndex(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            ref Guid subGroupOfPowerSettingsGuid,
            ref Guid powerSettingGuid,
            out uint acValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadDCValueIndex(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            ref Guid subGroupOfPowerSettingsGuid,
            ref Guid powerSettingGuid,
            out uint dcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(
            IntPtr userRootPowerKey,
            out IntPtr activePolicyGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(
            IntPtr userRootPowerKey,
            ref Guid schemeGuid);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        // Constants
        public const uint ACCESSSCHEME = 16;
        public const uint ACCESSSUBGROUP = 17;
        public const uint ACCESSINDIVIDUALSETTING = 18;
        public const uint ERRORSUCCESS = 0;
        public const uint ERRORNOMOREITEMS = 259;
    }
}
