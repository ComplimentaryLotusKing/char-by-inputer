using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CharByInputer
{
    /// <summary>
    /// 通过 Win32 SendInput + KEYEVENTF_UNICODE 发送逐字键盘输入。
    /// 优点：真实键盘事件；中文/符号原生支持；不触发 paste 事件；不依赖 IME。
    /// </summary>
    internal static class TypeSender
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        // 特殊字符 → 虚拟键码（普通字符走 Unicode，仅这些特殊字符走 VK）
        private static readonly Dictionary<char, ushort> SpecialKeys = new Dictionary<char, ushort>
        {
            { '\n', 0x0D }, // VK_RETURN
            { '\r', 0x0D },
            { '\t', 0x09 }, // VK_TAB
            { '\b', 0x08 }, // VK_BACK
            { (char)0x1B, 0x1B } // VK_ESCAPE
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public HARDWAREINPUT hi;
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
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        /// <summary>发送一个字符。普通字符走 Unicode 键盘事件；\n / \t / \b 等走虚拟键。</summary>
        public static bool SendChar(char c)
        {
            ushort vk;
            if (SpecialKeys.TryGetValue(c, out vk))
                return SendVk(vk);
            return SendUnicode(c);
        }

        /// <summary>把整段文本一次性发送（不逐字延时，通常仅用于测试）。</summary>
        public static int SendText(string text)
        {
            int sent = 0;
            foreach (char c in text)
            {
                if (SendChar(c)) sent++;
            }
            return sent;
        }

        /// <summary>Unicode 键盘事件：按下 + 抬起。</summary>
        private static bool SendUnicode(char c)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = MakeUnicodeInput(c, false);
            inputs[1] = MakeUnicodeInput(c, true);
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }

        /// <summary>虚拟键事件：按下 + 抬起。</summary>
        private static bool SendVk(ushort vk)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = MakeVkInput(vk, false);
            inputs[1] = MakeVkInput(vk, true);
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }

        private static INPUT MakeUnicodeInput(char c, bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki = new KEYBDINPUT();
            input.U.ki.wScan = c;
            input.U.ki.dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            input.U.ki.dwExtraInfo = GetMessageExtraInfo();
            return input;
        }

        private static INPUT MakeVkInput(ushort vk, bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki = new KEYBDINPUT();
            input.U.ki.wVk = vk;
            input.U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            input.U.ki.dwExtraInfo = GetMessageExtraInfo();
            return input;
        }
    }
}