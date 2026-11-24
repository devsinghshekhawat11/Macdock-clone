using System.Runtime.InteropServices;

namespace MacDock.Interop.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY(Guid fmtid, uint pid)
{
    public Guid fmtid = fmtid;
    public uint pid = pid;
}

/// <summary>
/// PROPVARIANT laid out without unsafe code: 8 bytes of header, then a 2-pointer union
/// (16 bytes on x86, 24 on x64, matching the native size). Only VT_LPWSTR is read here.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PROPVARIANT
{
    public ushort vt;
    public ushort reserved1, reserved2, reserved3;
    public IntPtr p;
    public IntPtr p2;
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
    [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
    [PreserveSig] int Commit();
}

internal static class AppUserModel
{
    private const ushort VT_LPWSTR = 31;
    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    private static PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    /// <summary>The window's explicit AppUserModelID, or <c>null</c>.</summary>
    public static string? GetWindowAppId(IntPtr hwnd)
    {
        var iid = IID_IPropertyStore;
        IPropertyStore? store = null;
        try
        {
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store is null) return null;
            if (store.GetValue(ref PKEY_AppUserModel_ID, out var value) != 0) return null;
            try
            {
                return value.vt == VT_LPWSTR && value.p != IntPtr.Zero ? Marshal.PtrToStringUni(value.p) : null;
            }
            finally { PropVariantClear(ref value); }
        }
        catch (COMException) { return null; }
        finally
        {
            if (store is not null) Marshal.FinalReleaseComObject(store);
        }
    }
}
