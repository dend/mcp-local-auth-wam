using System.Runtime.InteropServices;

namespace Den.Dev.LocalMCP.WAM.Win32
{
    static partial class NativeBridge
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public nint Reserved1;
            public nint PebBaseAddress;
            public nint Reserved2_0;
            public nint Reserved2_1;
            public nint UniqueProcessId;
            public nint InheritedFromUniqueProcessId;
        }
    }
}
