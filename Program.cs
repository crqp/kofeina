using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kofeina;

internal static unsafe class Program
{
    private const string WindowClassName = "KofeinaMessageWindow";
    private const uint MenuToggle = 1;
    private const uint MenuExit = 2;

    private static readonly NativeMethods.WindowProcedure s_wndProc = WndProc;
    private static nint s_hWnd;
    private static bool s_awake;

    [STAThread]
    private static void Main()
    {
        using Mutex mutex = new(true, @"Local\kofeina-tray", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        s_hWnd = CreateMessageWindow();
        TrayIcon.Add(s_hWnd, awake: false);

        while (NativeMethods.GetMessageW(out NativeMethods.Message message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(in message);
            NativeMethods.DispatchMessageW(in message);
        }
    }

    private static nint CreateMessageWindow()
    {
        nint hInstance = NativeMethods.GetModuleHandleW(null);

        fixed (char* className = WindowClassName)
        {
            NativeMethods.WindowClassEx windowClass = new()
            {
                cbSize = (uint)sizeof(NativeMethods.WindowClassEx),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = hInstance,
                lpszClassName = (nint)className,
            };

            if (NativeMethods.RegisterClassExW(in windowClass) == 0)
            {
                throw new Win32Exception();
            }
        }

        nint hWnd = NativeMethods.CreateWindowExW(
            0,
            WindowClassName,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            hInstance,
            0
        );
        if (hWnd == 0)
        {
            throw new Win32Exception();
        }

        return hWnd;
    }

    private static nint WndProc(nint hWnd, uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case NativeMethods.WM_TRAYICON:
                switch ((nuint)lParam)
                {
                    case NativeMethods.WM_LBUTTONUP:
                        Toggle();
                        break;
                    case NativeMethods.WM_RBUTTONUP:
                        ShowMenu();
                        break;
                }

                return 0;

            case NativeMethods.WM_DESTROY:
                TrayIcon.Remove();
                NativeMethods.PostQuitMessage(0);
                return 0;

            default:
                return NativeMethods.DefWindowProcW(hWnd, message, wParam, lParam);
        }
    }

    private static void Toggle()
    {
        bool awake = !s_awake;
        uint flags = awake
            ? NativeMethods.ES_CONTINUOUS
                | NativeMethods.ES_DISPLAY_REQUIRED
                | NativeMethods.ES_SYSTEM_REQUIRED
            : NativeMethods.ES_CONTINUOUS;
        if (NativeMethods.SetThreadExecutionState(flags) == 0)
        {
            throw new Win32Exception();
        }

        s_awake = awake;
        TrayIcon.SetState(s_awake);
    }

    private static void ShowMenu()
    {
        nint menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        NativeMethods.AppendMenuW(
            menu,
            NativeMethods.MF_STRING | (s_awake ? NativeMethods.MF_CHECKED : 0u),
            MenuToggle,
            s_awake ? "Toggle off" : "Toggle on"
        );
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, 0, null);
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, MenuExit, "Exit");

        NativeMethods.GetCursorPos(out NativeMethods.Point point);
        NativeMethods.SetForegroundWindow(s_hWnd);
        uint command = NativeMethods.TrackPopupMenu(
            menu,
            NativeMethods.TPM_RIGHTBUTTON
                | NativeMethods.TPM_NONOTIFY
                | NativeMethods.TPM_RETURNCMD,
            point.x,
            point.y,
            0,
            s_hWnd,
            0
        );
        NativeMethods.PostMessageW(s_hWnd, NativeMethods.WM_NULL, 0, 0);
        NativeMethods.DestroyMenu(menu);

        switch (command)
        {
            case MenuToggle:
                Toggle();
                break;
            case MenuExit:
                NativeMethods.DestroyWindow(s_hWnd);
                break;
        }
    }
}
