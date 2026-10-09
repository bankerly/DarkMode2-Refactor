using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DarkMode.Ui.Services;

/// <summary>
/// Windows 11 acrylic backdrop: the desktop behind the window is blurred by DWM and our
/// translucent surface colour is composited on top of it (colour mixing).
/// Falls back to the legacy accent API on older builds.
/// </summary>
public static class WindowBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int BackdropAcrylic = 3;
    private const int CornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Returns false when neither DWM backdrop nor the accent API could be used.</summary>
    public static bool Apply(Window window, bool dark, Color tint)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;

        var darkMode = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        var corner = CornerRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var backdrop = BackdropAcrylic;
        if (DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0)
        {
            return true;
        }

        return EnableAcrylicFallback(handle, tint);
    }

    // ---- legacy path (Windows 10 1803+) -------------------------------------
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AccentFlagDrawBorder = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;   // AABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private static bool EnableAcrylicFallback(IntPtr handle, Color tint)
    {
        var policy = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = AccentFlagDrawBorder,
            GradientColor = (0xB4 << 24) | (tint.B << 16) | (tint.G << 8) | tint.R
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new WindowCompositionAttributeData { Attribute = WcaAccentPolicy, Data = buffer, SizeOfData = size };
            return SetWindowCompositionAttribute(handle, ref data) != 0;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
