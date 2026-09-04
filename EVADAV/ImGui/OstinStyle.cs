using EVADAV.Theme;
using ImGuiNET;
using System.Numerics;
using Color = System.Windows.Media.Color;

namespace EVADAV.ImGuiUi
{
    internal static class OstinStyle
    {
        public static Vector4 Main = Rgba(0, 210, 106);
        public static Vector4 MainSoft = Rgba(0, 210, 106, 180);
        public static Vector4 MainActive = Rgba(0, 150, 76);
        public static readonly Vector4 Text = Rgba(255, 255, 255);
        public static readonly Vector4 TextDim = Rgba(255, 255, 255, 127);
        public static readonly Vector4 TextMuted = Rgba(186, 186, 186);
        public static readonly Vector4 Window = Rgba(7, 7, 7, 255);
        public static readonly Vector4 Sidebar = Rgba(0, 0, 0, 140);
        public static readonly Vector4 Child = Rgba(12, 12, 13, 180);
        public static readonly Vector4 TabActive = Rgba(11, 11, 11);
        public static readonly Vector4 TabIdle = Rgba(0, 0, 0);
        public static readonly Vector4 Frame = Rgba(20, 24, 27);
        public static readonly Vector4 Stroke = Rgba(27, 27, 29);
        public static readonly Vector4 CheckBg = Rgba(42, 46, 50);

        public const float WindowW = 1050f;
        public const float WindowH = 650f;
        public const float SidebarW = 250f;
        public const float WindowRounding = 16f;

        public static Vector4 Rgba(int r, int g, int b, int a = 255) =>
            new(r / 255f, g / 255f, b / 255f, a / 255f);

        public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

        public static uint U32(int r, int g, int b, int a = 255) => U32(Rgba(r, g, b, a));

        public static void SetAccent(float r, float g, float b)
        {
            r = Math.Clamp(r, 0f, 1f);
            g = Math.Clamp(g, 0f, 1f);
            b = Math.Clamp(b, 0f, 1f);
            Main = new Vector4(r, g, b, 1f);
            MainSoft = new Vector4(r, g, b, 0.7f);
            MainActive = new Vector4(r * 0.72f, g * 0.72f, b * 0.72f, 1f);
        }

        public static void SetAccent(Color color) =>
            SetAccent(color.R / 255f, color.G / 255f, color.B / 255f);

        public static void Apply()
        {
            var s = ImGui.GetStyle();
            s.WindowRounding = WindowRounding;
            s.ChildRounding = 8f;
            s.FrameRounding = 4f;
            s.GrabRounding = 4f;
            s.PopupRounding = 8f;
            s.ScrollbarRounding = 5f;
            s.WindowPadding = Vector2.Zero;
            s.WindowBorderSize = 0f;
            s.ChildBorderSize = 0f;
            s.FrameBorderSize = 0f;
            s.ItemSpacing = new Vector2(10, 8);
            s.ScrollbarSize = 7f;

            var c = s.Colors;
            c[(int)ImGuiCol.WindowBg] = Window;
            c[(int)ImGuiCol.ChildBg] = new Vector4(0, 0, 0, 0);
            c[(int)ImGuiCol.PopupBg] = Rgba(7, 8, 18, 240);
            c[(int)ImGuiCol.Border] = new Vector4(0, 0, 0, 0);
            c[(int)ImGuiCol.Text] = Text;
            c[(int)ImGuiCol.TextDisabled] = TextDim;
            c[(int)ImGuiCol.FrameBg] = Frame;
            c[(int)ImGuiCol.FrameBgHovered] = Rgba(32, 36, 42);
            c[(int)ImGuiCol.FrameBgActive] = Rgba(40, 44, 52);
            c[(int)ImGuiCol.CheckMark] = Main;
            c[(int)ImGuiCol.SliderGrab] = Main;
            c[(int)ImGuiCol.SliderGrabActive] = MainSoft;
            c[(int)ImGuiCol.Button] = Frame;
            c[(int)ImGuiCol.ButtonHovered] = Main;
            c[(int)ImGuiCol.ButtonActive] = MainActive;
            c[(int)ImGuiCol.Header] = TabActive;
            c[(int)ImGuiCol.HeaderHovered] = Rgba(24, 24, 28);
            c[(int)ImGuiCol.HeaderActive] = Main;
            c[(int)ImGuiCol.Separator] = Stroke;
            c[(int)ImGuiCol.ScrollbarBg] = Rgba(15, 15, 16);
            c[(int)ImGuiCol.ScrollbarGrab] = Rgba(40, 40, 48);
            c[(int)ImGuiCol.ScrollbarGrabHovered] = MainSoft;
            c[(int)ImGuiCol.TitleBg] = Window;
            c[(int)ImGuiCol.TitleBgActive] = Window;
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) =>
            new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), Lerp(a.W, b.W, t));
    }
}
