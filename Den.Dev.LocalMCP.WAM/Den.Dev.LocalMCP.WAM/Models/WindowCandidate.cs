namespace Den.Dev.LocalMCP.WAM.Models
{
    internal class WindowCandidate
    {
        public nint Handle { get; set; }
        public int ProcessId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Size { get; set; }
    }
}
