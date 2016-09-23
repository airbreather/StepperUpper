using System.Runtime.InteropServices;

namespace StepperUpper
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WritePrivateProfileString(string sectionName, string propertyName, string value, string iniFilePath);
    }
}
