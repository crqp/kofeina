using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kofeina;

internal static unsafe class TrayIcon
{
    private const uint IconId = 1;
    private const int Size = 16;
    private const double OuterRadius = 6.6;
    private const double InnerRadius = 4.9;

    private static nint s_hWnd;
    private static nint s_onIcon;
    private static nint s_offIcon;

    internal static void Add(nint hWnd, bool awake)
    {
        s_hWnd = hWnd;
        s_onIcon = CreateStateIcon(active: true);
        s_offIcon = CreateStateIcon(active: false);

        NativeMethods.NotifyIconData data = CreateData(awake);
        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data))
        {
            throw new Win32Exception();
        }
    }

    internal static void SetState(bool awake)
    {
        NativeMethods.NotifyIconData data = CreateData(awake);
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref data);
    }

    internal static void Remove()
    {
        NativeMethods.NotifyIconData data = default;
        data.cbSize = (uint)sizeof(NativeMethods.NotifyIconData);
        data.hWnd = s_hWnd;
        data.uID = IconId;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref data);

        NativeMethods.DestroyIcon(s_onIcon);
        NativeMethods.DestroyIcon(s_offIcon);
    }

    private static NativeMethods.NotifyIconData CreateData(bool awake)
    {
        NativeMethods.NotifyIconData data = default;
        data.cbSize = (uint)sizeof(NativeMethods.NotifyIconData);
        data.hWnd = s_hWnd;
        data.uID = IconId;
        data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        data.uCallbackMessage = NativeMethods.WM_TRAYICON;
        data.hIcon = awake ? s_onIcon : s_offIcon;

        ReadOnlySpan<char> tip = awake ? "kofeina: screen is being kept awake" : "kofeina: idle";
        char* buffer = data.szTip;
        tip.CopyTo(new Span<char>(buffer, 128));

        return data;
    }

    private static nint CreateStateIcon(bool active)
    {
        NativeMethods.BitmapInfoHeader header = new()
        {
            biSize = (uint)sizeof(NativeMethods.BitmapInfoHeader),
            biWidth = Size,
            biHeight = -Size,
            biPlanes = 1,
            biBitCount = 32,
        };

        nint colourBitmap = NativeMethods.CreateDIBSection(0, ref header, 0, out nint bits, 0, 0);
        if (colourBitmap == 0)
        {
            throw new Win32Exception();
        }

        DrawDisc(new Span<byte>((void*)bits, Size * Size * 4), active);

        byte* maskBits = stackalloc byte[64];
        NativeMemory.Clear(maskBits, 64);
        nint maskBitmap = NativeMethods.CreateBitmap(Size, Size, 1, 1, (nint)maskBits);
        if (maskBitmap == 0)
        {
            NativeMethods.DeleteObject(colourBitmap);
            throw new Win32Exception();
        }

        NativeMethods.IconInfo iconInfo = new()
        {
            fIcon = 1,
            hbmMask = maskBitmap,
            hbmColor = colourBitmap,
        };
        nint icon = NativeMethods.CreateIconIndirect(in iconInfo);
        NativeMethods.DeleteObject(colourBitmap);
        NativeMethods.DeleteObject(maskBitmap);
        if (icon == 0)
        {
            throw new Win32Exception();
        }

        return icon;
    }

    private static void DrawDisc(Span<byte> pixels, bool active)
    {
        byte red = active ? (byte)76 : (byte)158;
        byte green = active ? (byte)175 : (byte)158;
        byte blue = active ? (byte)80 : (byte)158;
        double inner = active ? 0.0 : InnerRadius;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int samples = 0;
                for (int sy = 0; sy < 4; sy++)
                {
                    for (int sx = 0; sx < 4; sx++)
                    {
                        double dx = x + (sx + 0.5) / 4.0 - Size / 2.0;
                        double dy = y + (sy + 0.5) / 4.0 - Size / 2.0;
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance <= OuterRadius && distance >= inner)
                        {
                            samples++;
                        }
                    }
                }

                int alpha = samples * 255 / 16;
                int offset = (y * Size + x) * 4;
                pixels[offset] = (byte)(blue * alpha / 255);
                pixels[offset + 1] = (byte)(green * alpha / 255);
                pixels[offset + 2] = (byte)(red * alpha / 255);
                pixels[offset + 3] = (byte)alpha;
            }
        }
    }
}
