using System.Runtime.InteropServices;

namespace Den.Dev.LocalMCP.WAM.Win32
{
    static partial class NativeBridge
    {        
        [DllImport("user32.dll")]
        internal static extern IntPtr GetDesktopWindow();

        [DllImport("ntdll.dll")]
        internal static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);
        
        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        [DllImport("user32.dll", SetLastError=true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        
        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        internal static int GetParentProcessId(int processId)
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            var pbi = new PROCESS_BASIC_INFORMATION();
            int returnLength;
            int status = NtQueryInformationProcess(process.Handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);

            if (status != 0)
            {
                throw new InvalidOperationException($"NtQueryInformationProcess failed with status code {status}");
            }

            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
    }
}
