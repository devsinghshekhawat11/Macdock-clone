using System.Runtime.InteropServices;

// Resolve Win32 DLLs only from System32, never from the app folder (prevents DLL planting and
// avoids probing our own directory when a P/Invoke is first bound).
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
