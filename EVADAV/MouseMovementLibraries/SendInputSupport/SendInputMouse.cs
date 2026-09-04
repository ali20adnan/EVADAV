using System.Runtime.InteropServices;

namespace MouseMovementLibraries.SendInputSupport
{
    internal class SendInputMouse
    {
        [DllImport("user32.dll")]
        private static extern void SendInput(int nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
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

        public static void SendMouseCommand(uint MouseCommand, int x = 0, int y = 0)
        {
            INPUT input = new INPUT
            {
                type = 0,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = x,
                        dy = y,
                        dwFlags = MouseCommand
                    }
                }
            };

            SendInput(1, [input], Marshal.SizeOf(typeof(INPUT)));
        }
    }
}