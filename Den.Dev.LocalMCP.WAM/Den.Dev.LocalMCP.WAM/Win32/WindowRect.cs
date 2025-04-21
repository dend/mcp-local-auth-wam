using System.Runtime.InteropServices;

namespace Den.Dev.LocalMCP.WAM.Win32
{
    static partial class NativeBridge
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public bool IsValidSize => Width > 50 && Height > 50;
        }
    }
}
