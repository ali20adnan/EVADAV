using ImGuiNET;
using System.Numerics;

namespace EVADAV.ImGuiUi
{
    internal static class OstinWidgets
    {
        private static readonly Dictionary<string, Vector4> TabIcon = new();
        private static readonly Dictionary<string, Vector4> TabBg = new();

        public static unsafe bool Tab(string label, string icon, bool selected, Vector2 size, ImFontPtr iconFont)
        {
            var window = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var id = "##tab_" + label;
            bool pressed = ImGui.InvisibleButton(id, size);
            bool hovered = ImGui.IsItemHovered();

            float dt = ImGui.GetIO().DeltaTime * 14f;
            if (!TabIcon.TryGetValue(label, out var iconCol))
                iconCol = OstinStyle.Text;
            if (!TabBg.TryGetValue(label, out var bgCol))
                bgCol = OstinStyle.TabIdle;

            var targetIcon = selected ? OstinStyle.Main : OstinStyle.Text;
            var targetBg = selected || hovered ? OstinStyle.TabActive : OstinStyle.TabIdle;
            iconCol = OstinStyle.Lerp(iconCol, targetIcon, dt);
            bgCol = OstinStyle.Lerp(bgCol, targetBg, dt);
            TabIcon[label] = iconCol;
            TabBg[label] = bgCol;

            window.AddRectFilled(pos, pos + size, OstinStyle.U32(bgCol), 8f);
            if (selected)
                window.AddRectFilled(pos, pos + new Vector2(3, size.Y), OstinStyle.U32(OstinStyle.Main), 2f);

            var iconPos = pos + new Vector2(16, 14);
            if (iconFont.NativePtr != null)
                window.AddText(iconFont, 18f, iconPos, OstinStyle.U32(iconCol), icon);
            else
                window.AddText(ImGui.GetFont(), 18f, iconPos, OstinStyle.U32(iconCol), icon);

            window.AddText(ImGui.GetFont(), 18f, pos + new Vector2(50, 16), OstinStyle.U32(OstinStyle.Text), label);
            return pressed;
        }

        public static void RoundedAccentBorder(ImDrawListPtr draw, Vector2 p, Vector2 size, float rounding, float thickness = 1.6f)
        {
            var pad = thickness * 0.5f;
            draw.AddRect(
                p + new Vector2(pad, pad),
                p + size - new Vector2(pad, pad),
                OstinStyle.U32(OstinStyle.Main),
                Math.Max(0f, rounding - pad),
                ImDrawFlags.RoundCornersAll,
                thickness);
        }

        public static void BeginPanel(string id, Vector2 size)
        {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + size, OstinStyle.U32(OstinStyle.Child), 8f);
            ImGui.BeginChild(id, size, ImGuiChildFlags.None, ImGuiWindowFlags.None);
            ImGui.Dummy(new Vector2(0, 6));
            ImGui.Indent(14);
        }

        public static void EndPanel()
        {
            ImGui.Unindent(14);
            ImGui.EndChild();
        }

        public static void SectionLabel(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, OstinStyle.Main);
            ImGui.Text(text);
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2(0, 2));
        }

        public static bool Checkbox(string label, ref bool value)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, OstinStyle.CheckBg);
            bool changed = ImGui.Checkbox(label, ref value);
            ImGui.PopStyleColor();
            return changed;
        }

        public static bool SliderFloat(string label, ref float value, float min, float max, string format)
        {
            ImGui.TextColored(OstinStyle.TextMuted, label);
            ImGui.SetNextItemWidth(-12);
            return ImGui.SliderFloat("##" + label, ref value, min, max, format);
        }

        public static bool Combo(string label, ref int index, string[] items)
        {
            ImGui.TextColored(OstinStyle.TextMuted, label);
            ImGui.SetNextItemWidth(-12);
            return ImGui.Combo("##" + label, ref index, items, items.Length);
        }

        public static bool Button(string label, Vector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, OstinStyle.Frame);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, OstinStyle.Main);
            bool pressed = ImGui.Button(label, size);
            ImGui.PopStyleColor(2);
            return pressed;
        }

        public static bool ColorRow(string label, ref Vector4 color)
        {
            ImGui.TextColored(OstinStyle.TextMuted, label);
            ImGui.SameLine(ImGui.GetWindowWidth() - 86);
            return ImGui.ColorEdit4("##" + label, ref color,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoLabel);
        }

        public static bool KeyRow(string label, string display)
        {
            ImGui.TextColored(OstinStyle.TextMuted, label);
            ImGui.SameLine(ImGui.GetWindowWidth() - 110);
            ImGui.PushStyleColor(ImGuiCol.Button, OstinStyle.Rgba(22, 22, 30));
            bool pressed = ImGui.Button(display + "##" + label, new Vector2(90, 22));
            ImGui.PopStyleColor();
            return pressed;
        }
    }
}
