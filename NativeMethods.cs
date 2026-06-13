using System.Runtime.InteropServices;
using System.Text;

namespace LockPicker;

/// <summary>
/// Обёртка над WinAPI: поиск окна игры, активация и отправка клавиш через SendInput
/// со скан-кодами (это необходимо для совместимости с большинством игр на DirectInput).
/// </summary>
internal static class NativeMethods
{
    // ===== Поиск и активация окна =====

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Ищет первое видимое окно, чей заголовок содержит указанную подстроку (без учёта регистра).
    /// </summary>
    public static IntPtr FindWindowByTitleContains(string titlePart)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true; // продолжаем перебор
            }

            int length = GetWindowTextLength(hWnd);
            if (length == 0)
            {
                return true;
            }

            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (title.Contains(titlePart, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false; // нашли — останавливаем перебор
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>
    /// Выводит окно на передний план (при необходимости разворачивает из свёрнутого состояния).
    /// </summary>
    public static void BringWindowToFront(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
        }

        SetForegroundWindow(hWnd);
    }

    // ===== Отправка клавиш через SendInput =====

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MAPVK_VK_TO_VSC = 0;

    /// <summary>
    /// Нажимает и отпускает клавишу по виртуальному коду, используя скан-код
    /// (надёжнее для игр). holdMs — длительность удержания клавиши.
    /// </summary>
    public static void TapKey(ushort virtualKey, int holdMs = 40)
    {
        ushort scanCode = (ushort)MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);

        SendKeyEvent(scanCode, isKeyUp: false);
        Thread.Sleep(holdMs);
        SendKeyEvent(scanCode, isKeyUp: true);
    }

    private static void SendKeyEvent(ushort scanCode, bool isKeyUp)
    {
        uint flags = KEYEVENTF_SCANCODE;
        if (isKeyUp)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        var inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki = new KEYBDINPUT
        {
            wVk = 0,
            wScan = scanCode,
            dwFlags = flags,
            time = 0,
            dwExtraInfo = IntPtr.Zero
        };

        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }
}
