using ClickableTransparentOverlay;
using EVADAV.Class;
using ImGuiNET;
using InputLogic;
using MouseMovementLibraries.ArduinoSupport;
using Other;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Visuality;

namespace EVADAV.ImGuiUi
{
    internal sealed class EvadavOverlay : Overlay
    {
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        private readonly AppController _app;
        private bool _menuOpen = true;
        private int _page;
        private int _activeTab;
        private float _tabAlpha = 1f;
        private float _titleSlide;
        private string? _listeningBind;
        private string _modelFilter = "";
        private string _configFilter = "";
        private bool _showSave;
        private string _saveName = "MyConfig";
        private string _saveModel = "";
        private readonly Dictionary<string, Vector4> _colorCache = new();
        private string[] _arduinoPorts = ["AUTO"];
        private DateTime _arduinoRefresh;

        private static readonly string[] Tabs = ["Aim", "Visuals", "Models", "Settings", "About"];
        private static readonly string[] TabIcons =
        [
            "\uf05b", // crosshairs
            "\uf06e", // eye
            "\uf1b2", // cube
            "\uf013", // gear
            "\uf05a"  // info
        ];
        private static readonly ushort[] IconGlyphRanges = [0xf000, 0xf8ff, 0];
        private static readonly GCHandle IconRangePin = GCHandle.Alloc(IconGlyphRanges, GCHandleType.Pinned);
        private ImFontPtr _iconFont;
        private static readonly string[] PredictionMethods = ["Kalman Filter", "Frame Lead", "EMA Prediction"];
        private static readonly string[] DetectionAreas = ["Closest to Center Screen", "Closest to Mouse"];
        private static readonly string[] Alignments = ["Center", "Top", "Bottom"];
        private static readonly string[] MovementPaths = ["Cubic Bezier", "Exponential", "Linear", "Adaptive", "Perlin Noise"];
        private static readonly string[] MouseMethods =
        [
            "Mouse Event", "SendInput", "LG HUB",
            "Razer Synapse (Require Razer Peripheral)",
            "ddxoft Virtual Input Driver",
            ArduinoMouse.MethodName
        ];
        private static readonly string[] CaptureMethods = ["DirectX", "GDI+"];
        private static readonly string[] ImageSizes = ["640", "512", "416", "320", "256", "160"];
        private static readonly string[] TracerPositions = ["Top", "Middle", "Bottom"];
        private static readonly string[] FovStyles = ["Circle", "Rectangle"];

        public EvadavOverlay(AppController app) : base("EVADAV", true, 1920, 1080)
        {
            _app = app;
            VSync = true;
            FPSLimit = 144;
            app.BindingManager.OnBindingSet += (id, key) =>
            {
                Dictionary.bindingSettings[id] = key;
                if (_listeningBind == id)
                    _listeningBind = null;
            };
        }

        protected override unsafe Task PostInitialized()
        {
            int w = GetSystemMetrics(0);
            int h = GetSystemMetrics(1);
            Size = new System.Drawing.Size(w, h);
            Position = new System.Drawing.Point(0, 0);

            ReplaceFont(config =>
            {
                var io = ImGui.GetIO();
                if (File.Exists(AppController.MenuFontPath))
                    io.Fonts.AddFontFromFileTTF(AppController.MenuFontPath, 17f, config);
                else
                    io.Fonts.AddFontDefault(config);

                if (File.Exists(AppController.IconFontPath))
                {
                    var iconCfg = *config;
                    iconCfg.MergeMode = 0;
                    iconCfg.PixelSnapH = 1;
                    iconCfg.GlyphMinAdvanceX = 16f;
                    _iconFont = io.Fonts.AddFontFromFileTTF(
                        AppController.IconFontPath,
                        18f,
                        &iconCfg,
                        IconRangePin.AddrOfPinnedObject());
                }
            });

            return Task.CompletedTask;
        }

        protected override void Render()
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Insert, false))
                _menuOpen = !_menuOpen;

            OstinStyle.Apply();
            if (On("Show Watermark"))
                DrawWatermark();

            if (!_menuOpen)
                return;

            var io = ImGui.GetIO();
            _tabAlpha = OstinStyle.Lerp(_tabAlpha, _page == _activeTab ? 1f : 0f, 18f * io.DeltaTime);
            if (_tabAlpha < 0.01f) _activeTab = _page;
            _titleSlide = OstinStyle.Lerp(_titleSlide, _page == _activeTab ? 20f : 0f, 14f * io.DeltaTime);

            ImGui.SetNextWindowSize(new Vector2(OstinStyle.WindowW, OstinStyle.WindowH), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2((io.DisplaySize.X - OstinStyle.WindowW) * 0.5f, (io.DisplaySize.Y - OstinStyle.WindowH) * 0.5f), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, OstinStyle.WindowRounding);

            ImGui.Begin("General", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar);
            {
                var draw = ImGui.GetWindowDrawList();
                var p = ImGui.GetWindowPos();
                var region = ImGui.GetWindowSize();
                float round = OstinStyle.WindowRounding;

                var bg = ImGui.GetBackgroundDrawList();
                for (int i = 10; i >= 1; i--)
                {
                    float o = i * 1.6f;
                    bg.AddRectFilled(p - new Vector2(o, o), p + region + new Vector2(o, o), OstinStyle.U32(0, 0, 0, 10), round + o);
                }

                draw.AddRectFilled(p, p + new Vector2(OstinStyle.SidebarW, region.Y), OstinStyle.U32(OstinStyle.Sidebar), round, ImDrawFlags.RoundCornersLeft);
                OstinWidgets.RoundedAccentBorder(draw, p, region, round);

                draw.AddText(ImGui.GetFont(), 34f, p + new Vector2(27, 32), OstinStyle.U32(OstinStyle.Main), "EVADAV");
                float titleW = ImGui.GetFont().CalcTextSizeA(34f, float.MaxValue, 0, "EVADAV").X;
                draw.AddRectFilled(p + new Vector2(27, 70), p + new Vector2(27 + titleW, 73), OstinStyle.U32(OstinStyle.Main), 1.5f);

                ImGui.SetCursorPos(new Vector2(8, 112));
                ImGui.BeginGroup();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 8));
                for (int i = 0; i < Tabs.Length; i++)
                {
                    if (OstinWidgets.Tab(Tabs[i], TabIcons[i], i == _page, new Vector2(234, 50), _iconFont))
                        _page = i;
                }
                ImGui.PopStyleVar();
                ImGui.EndGroup();

                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _tabAlpha * ImGui.GetStyle().Alpha);
                draw.AddText(ImGui.GetFont(), 23f, p + new Vector2(246 + _titleSlide, 18), OstinStyle.U32(OstinStyle.Text), $"[{Tabs[_activeTab]}]");

                switch (_activeTab)
                {
                    case 0: DrawAim(); break;
                    case 1: DrawVisuals(); break;
                    case 2: DrawModels(); break;
                    case 3: DrawSettings(); break;
                    case 4: DrawAbout(); break;
                }
                ImGui.PopStyleVar();
            }
            ImGui.End();
            ImGui.PopStyleVar();

            if (_showSave)
                DrawSavePopup();
        }

        private void DrawWatermark()
        {
            ImGui.SetNextWindowSize(new Vector2(430, 50));
            ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
            ImGui.Begin("##watermark", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBringToFrontOnFocus);
            var p = ImGui.GetWindowPos();
            var draw = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var wsize = ImGui.GetWindowSize();
            OstinWidgets.RoundedAccentBorder(draw, p, wsize, 12f, 1.4f);
            draw.AddText(font, 17f, p + new Vector2(16, 16), OstinStyle.U32(OstinStyle.Main), "EVADAV");
            float brandW = font.CalcTextSizeA(17f, float.MaxValue, 0, "EVADAV").X;
            draw.AddText(font, 17f, p + new Vector2(22 + brandW, 16), OstinStyle.U32(OstinStyle.Text), "Ostin");
            var fps = ImGui.GetIO().Framerate.ToString("0");
            draw.AddText(font, 17f, p + new Vector2(90 + brandW, 16), OstinStyle.U32(OstinStyle.Text), fps);
            draw.AddText(font, 15f, p + new Vector2(90 + brandW + ImGui.CalcTextSize(fps).X + 6, 18), OstinStyle.U32(OstinStyle.TextDim), "FPS");
            draw.AddText(font, 15f, p + new Vector2(210 + brandW, 18), OstinStyle.U32(OstinStyle.TextDim), _menuOpen ? "INSERT hide" : "INSERT show");
            ImGui.End();
            ImGui.PopStyleVar();
        }

        private void DrawAim()
        {
            ImGui.SetCursorPos(new Vector2(266, 76));
            OstinWidgets.BeginPanel("aim_assist", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Aim Assist");
            Toggle("Aim Assist");
            Toggle("Constant AI Tracking");
            Toggle("Sticky Aim");
            if (On("Sticky Aim"))
                Slider("Sticky Aim Threshold", 0, 100, "%.0f px");
            Keybind("Aim Keybind");
            Keybind("Second Aim Keybind");
            OstinWidgets.SectionLabel("Auto Trigger");
            Toggle("Auto Trigger");
            Toggle("Cursor Check");
            Toggle("Spray Mode");
            Slider("Auto Trigger Delay", 0.01f, 1f, "%.2f s");
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(266, 342));
            OstinWidgets.BeginPanel("aim_pred", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Predictions");
            Toggle("Predictions");
            Combo("Prediction Method", PredictionMethods);
            if (Val("Prediction Method") == "Kalman Filter")
                Slider("Kalman Lead Time", 0.02f, 0.30f, "%.2f s");
            else if (Val("Prediction Method") is "Frame Lead" or "Shall0e's Prediction")
                Slider("Shalloe Lead Multiplier", 1f, 10f, "%.1f");
            else
                Slider("WiseTheFox Lead Time", 0.02f, 0.30f, "%.2f s");
            Toggle("EMA Smoothening");
            if (Slider("EMA Smoothening", 0.01f, 1f, "%.2f"))
                _app.OnEmaChanged(Num("EMA Smoothening"));
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(658, 76));
            OstinWidgets.BeginPanel("aim_cfg", new Vector2(376, 516));
            OstinWidgets.SectionLabel("Aim Config");
            if (Combo("Mouse Movement Method", MouseMethods))
                _ = _app.OnMouseMethodChanged(Val("Mouse Movement Method"));
            RefreshArduinoPorts();
            if (Combo("Arduino COM Port", _arduinoPorts))
                _ = _app.ReloadArduino();
            if (OstinWidgets.Button("Test Arduino Move", new Vector2(-12, 28)))
                _app.TestArduino();
            Combo("Movement Path", MovementPaths);
            Combo("Detection Area Type", DetectionAreas);
            Combo("Aiming Boundaries Alignment", Alignments);
            if (Slider("Mouse Sensitivity (+/-)", 0.01f, 1f, "%.2f"))
            {
                var v = Num("Mouse Sensitivity (+/-)");
                if (v >= 0.98)
                    LogManager.Log(LogManager.LogLevel.Warning, "The Mouse Sensitivity you have set can cause EVADAV to be unable to aim, please decrease if you suffer from this problem", true);
                else if (v <= 0.1)
                    LogManager.Log(LogManager.LogLevel.Warning, "The Mouse Sensitivity you have set can cause EVADAV to be unstable to aim, please increase if you suffer from this problem", true);
            }
            Slider("Mouse Jitter", 0, 15, "%.0f");
            Toggle("Y Axis Percentage Adjustment");
            Toggle("X Axis Percentage Adjustment");
            if (!On("Y Axis Percentage Adjustment"))
                Slider("Y Offset (Up/Down)", -150, 150, "%.0f");
            else
                Slider("Y Offset (%)", 0, 100, "%.0f");
            if (!On("X Axis Percentage Adjustment"))
                Slider("X Offset (Left/Right)", -150, 150, "%.0f");
            else
                Slider("X Offset (%)", 0, 100, "%.0f");
            OstinWidgets.EndPanel();
        }

        private void DrawVisuals()
        {
            ImGui.SetCursorPos(new Vector2(266, 76));
            OstinWidgets.BeginPanel("fov", new Vector2(376, 516));
            OstinWidgets.SectionLabel("FOV");
            Toggle("FOV");
            Toggle("Dynamic FOV");
            Toggle("Third Person Support");
            Keybind("Dynamic FOV Keybind");
            if (Combo("FOV Style", FovStyles))
                _app.ApplyFovStyle();
            if (Color("FOV Color"))
            {
                var c = _colorCache["FOV Color"];
                _app.ApplyFovColor(c.X, c.Y, c.Z, c.W);
            }
            if (Slider("FOV Size", 10, 640, "%.0f"))
                _app.OnFovSizeChanged(Num("FOV Size"));
            if (Slider("Dynamic FOV Size", 10, 640, "%.0f"))
                _app.OnDynamicFovSizeChanged(Num("Dynamic FOV Size"));
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(658, 76));
            OstinWidgets.BeginPanel("esp", new Vector2(376, 516));
            OstinWidgets.SectionLabel("ESP");
            Toggle("Show Detected Player");
            Toggle("Show AI Confidence");
            Toggle("Show Tracers");
            Combo("Tracer Position", TracerPositions);
            if (Color("Detected Player Color"))
            {
                var c = _colorCache["Detected Player Color"];
                _app.ApplyEspColor(c.X, c.Y, c.Z, c.W);
            }
            if (Slider("AI Confidence Font Size", 1, 30, "%.0f"))
                PropertyChanger.PostDPFontSize((int)Num("AI Confidence Font Size"));
            if (Slider("Corner Radius", 0, 100, "%.0f"))
                PropertyChanger.PostDPWCornerRadius((int)Num("Corner Radius"));
            if (Slider("Border Thickness", 0.1f, 10f, "%.1f"))
                PropertyChanger.PostDPWBorderThickness(Num("Border Thickness"));
            if (Slider("Opacity", 0, 1, "%.2f"))
                PropertyChanger.PostDPWOpacity(Num("Opacity"));
            OstinWidgets.EndPanel();
        }

        private void DrawModels()
        {
            ImGui.SetCursorPos(new Vector2(266, 76));
            OstinWidgets.BeginPanel("models", new Vector2(376, 516));
            OstinWidgets.SectionLabel("Local Models");
            ImGui.SetNextItemWidth(-12);
            ImGui.InputTextWithHint("##msearch", "Search models", ref _modelFilter, 128);
            var models = Filter(_app.FileStore.Models, _modelFilter);
            ImGui.SetNextItemWidth(-12);
            if (ImGui.BeginListBox("##modellist", new Vector2(-12, 360)))
            {
                for (int i = 0; i < models.Count; i++)
                {
                    bool selected = models[i] == Dictionary.lastLoadedModel;
                    if (ImGui.Selectable(models[i], selected))
                        _app.FileStore.SelectModel(models[i]);
                }
                ImGui.EndListBox();
            }
            ImGui.TextColored(OstinStyle.TextMuted, _app.FileStore.LoadedModelLabel);
            if (OstinWidgets.Button("Open Models Folder", new Vector2(-12, 28)))
                _app.OpenFolder("models");
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(658, 76));
            OstinWidgets.BeginPanel("configs", new Vector2(376, 516));
            OstinWidgets.SectionLabel("Local Configs");
            ImGui.SetNextItemWidth(-12);
            ImGui.InputTextWithHint("##csearch", "Search configs", ref _configFilter, 128);
            var configs = Filter(_app.FileStore.Configs, _configFilter);
            ImGui.SetNextItemWidth(-12);
            if (ImGui.BeginListBox("##cfglist", new Vector2(-12, 300)))
            {
                for (int i = 0; i < configs.Count; i++)
                {
                    bool selected = configs[i] == Dictionary.lastLoadedConfig;
                    if (ImGui.Selectable(configs[i], selected))
                        _app.FileStore.SelectConfig(configs[i]);
                }
                ImGui.EndListBox();
            }
            ImGui.TextColored(OstinStyle.TextMuted, _app.FileStore.LoadedConfigLabel);
            if (OstinWidgets.Button("Save Config", new Vector2(-12, 28)))
                _showSave = true;
            if (OstinWidgets.Button("Open Configs Folder", new Vector2(-12, 28)))
                _app.OpenFolder("configs");
            OstinWidgets.EndPanel();
        }

        private void DrawSettings()
        {
            ImGui.SetCursorPos(new Vector2(266, 76));
            OstinWidgets.BeginPanel("set_model", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Model Settings");
            if (Combo("Image Size", ImageSizes))
                _ = _app.ChangeImageSizeAsync(Val("Image Size"));
            Slider("AI FPS Limit", 0, 240, "%.0f FPS");
            var classes = new List<string> { "Best Confidence" };
            classes.AddRange(_app.ModelClasses.Values);
            Combo("Target Class", classes.ToArray());
            Slider("AI Minimum Confidence", 1, 100, "%.0f %%");
            Toggle("Enable Model Switch Keybind");
            Keybind("Model Switch Keybind");
            Keybind("Emergency Stop Keybind");
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(266, 342));
            OstinWidgets.BeginPanel("set_app", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Application");
            Toggle("Collect Data While Playing");
            Toggle("Auto Label Data");
            Toggle("Debug Mode");
            Toggle("StreamGuard");
            Combo("Screen Capture Method", CaptureMethods);
            if (OstinWidgets.Button("ddxoft DLL Locator", new Vector2(-12, 28)))
                _app.PickDdxoftDll();
            ImGui.TextColored(OstinStyle.TextDim, ShortPath(Dictionary.filelocationState["ddxoft DLL Location"]?.ToString() ?? ""));
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(658, 76));
            OstinWidgets.BeginPanel("set_screen", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Displays");
            var displays = DisplayManager.GetAllDisplays();
            for (int i = 0; i < displays.Count; i++)
            {
                bool current = i == DisplayManager.CurrentDisplayIndex;
                var label = displays[i].IsPrimary ? $"Display {i + 1} (Primary)" : $"Display {i + 1}";
                if (ImGui.RadioButton(label, current))
                    DisplayManager.SetDisplay(i);
            }
            if (OstinWidgets.Button("Performance Helper", new Vector2(-12, 28)))
                _app.ShowPerformanceHelper();
            OstinWidgets.EndPanel();

            ImGui.SetCursorPos(new Vector2(658, 342));
            OstinWidgets.BeginPanel("set_theme", new Vector2(376, 250));
            OstinWidgets.SectionLabel("Theme");
            var theme = OstinStyle.Main;
            if (OstinWidgets.ColorRow("Menu / overlay accent", ref theme))
            {
                _colorCache["Theme Color"] = theme;
                _app.ApplyThemeColor(theme.X, theme.Y, theme.Z);
            }
            Toggle("Show Watermark");
            if (OstinWidgets.Button("Exit EVADAV", new Vector2(-12, 28)))
                Close();
            OstinWidgets.EndPanel();
        }

        private void DrawAbout()
        {
            ImGui.SetCursorPos(new Vector2(266, 76));
            OstinWidgets.BeginPanel("about", new Vector2(768, 516));
            OstinWidgets.SectionLabel("EVADAV");
            ImGui.Text("Precision System");
            ImGui.TextColored(OstinStyle.Main, "v2.5.0");
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.TextWrapped(_app.SystemSpecs);
            ImGui.Dummy(new Vector2(0, 12));
            ImGui.TextColored(OstinStyle.TextDim, "Menu: Ostin ImGui  •  INSERT toggles the window");
            ImGui.Dummy(new Vector2(0, 16));
            if (OstinWidgets.Button("Check for Updates", new Vector2(200, 32)))
                _ = _app.CheckForUpdates();
            OstinWidgets.EndPanel();
        }

        private void DrawSavePopup()
        {
            ImGui.OpenPopup("Save Configuration");
            ImGui.SetNextWindowSize(new Vector2(420, 180));
            if (ImGui.BeginPopupModal("Save Configuration", ref _showSave, ImGuiWindowFlags.NoResize))
            {
                ImGui.InputText("Name", ref _saveName, 64);
                ImGui.InputText("Suggested model", ref _saveModel, 128);
                if (OstinWidgets.Button("Save", new Vector2(90, 28)))
                {
                    _app.SaveNamedConfig(_saveName, _saveModel);
                    _showSave = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (OstinWidgets.Button("Cancel", new Vector2(90, 28)))
                {
                    _showSave = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void Toggle(string key)
        {
            bool value = On(key);
            if (OstinWidgets.Checkbox(key, ref value))
            {
                Dictionary.toggleState[key] = value;
                _app.ApplyToggleSideEffects(key);
            }
        }

        private bool Slider(string key, float min, float max, string format)
        {
            float value = Convert.ToSingle(Dictionary.sliderSettings[key], CultureInfo.InvariantCulture);
            bool changed = OstinWidgets.SliderFloat(key, ref value, min, max, format);
            if (changed)
                Dictionary.sliderSettings[key] = (double)value;
            return changed;
        }

        private bool Combo(string key, string[] items)
        {
            string current = Val(key);
            int index = Array.FindIndex(items, x => x.Equals(current, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            bool changed = OstinWidgets.Combo(key, ref index, items);
            if (changed && index >= 0 && index < items.Length)
                Dictionary.dropdownState[key] = items[index];
            else if (!Dictionary.dropdownState.ContainsKey(key) && items.Length > 0)
                Dictionary.dropdownState[key] = items[0];
            return changed;
        }

        private void Keybind(string key)
        {
            string shown = _listeningBind == key ? "..." : KeybindNameManager.ConvertToRegularKey(Dictionary.bindingSettings[key].ToString());
            if (OstinWidgets.KeyRow(key, shown))
            {
                _listeningBind = key;
                _app.BindingManager.StartListeningForBinding(key);
            }
        }

        private bool Color(string key)
        {
            var col = ParseColor(key, Vector4.One);
            bool changed = OstinWidgets.ColorRow(key, ref col);
            if (changed)
                _colorCache[key] = col;
            return changed;
        }

        private Vector4 ParseColor(string key, Vector4 fallback)
        {
            if (_colorCache.TryGetValue(key, out var cached))
                return cached;
            try
            {
                var hex = Dictionary.colorState[key].ToString() ?? "";
                hex = hex.TrimStart('#');
                if (hex.StartsWith("FF") && hex.Length == 8) { }
                if (hex.Length >= 8)
                {
                    byte a = Convert.ToByte(hex[..2], 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    cached = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
                }
                else if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex[..2], 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    cached = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                }
                else cached = fallback;
            }
            catch { cached = fallback; }
            _colorCache[key] = cached;
            return cached;
        }

        private static bool On(string key) =>
            Dictionary.toggleState.TryGetValue(key, out var v) && Convert.ToBoolean(v);

        private static string Val(string key) =>
            Dictionary.dropdownState.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";

        private static double Num(string key) =>
            Convert.ToDouble(Dictionary.sliderSettings[key], CultureInfo.InvariantCulture);

        private static List<string> Filter(List<string> source, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return source;
            return source.Where(x => x.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "No file located";
            return path.Length < 42 ? path : "…" + path[^40..];
        }

        private void RefreshArduinoPorts()
        {
            if (DateTime.UtcNow - _arduinoRefresh < TimeSpan.FromSeconds(2))
                return;
            _arduinoRefresh = DateTime.UtcNow;
            var list = new List<string> { "AUTO" };
            foreach (var port in LeonardoPorts.List())
            {
                if (!list.Contains(port.Port))
                    list.Add(port.Port);
            }
            var saved = AimSettings.ArduinoComPort;
            if (!string.IsNullOrWhiteSpace(saved) && !list.Contains(saved))
                list.Add(saved);
            _arduinoPorts = list.ToArray();
        }
    }
}
