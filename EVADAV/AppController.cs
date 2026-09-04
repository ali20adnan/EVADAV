using EVADAV.AILogic;
using EVADAV.Class;
using EVADAV.MouseMovementLibraries.GHubSupport;
using EVADAV.Other;
using EVADAV.Theme;
using Class;
using InputLogic;
using MouseMovementLibraries.ArduinoSupport;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Visuality;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EVADAV
{
    public sealed class AppController
    {
        public static AppController Instance { get; private set; } = null!;

        private readonly Lazy<InputBindingManager> _bindingManager = new(() => new InputBindingManager());
        private static readonly Lazy<GithubManager> _githubManager = new(() => new GithubManager());
        private FileManager? _fileManager;
        private bool _performanceHelperOpen;
        private Window? _hiddenHost;
        private string _previousMouseMethod = "Mouse Event";

        private static readonly Lazy<FOV> _fovWindow = new(() =>
        {
            var window = new FOV();
            window.ForceReposition();
            return window;
        });

        private static readonly Lazy<DetectedPlayerWindow> _dpWindow = new(() =>
        {
            var window = new DetectedPlayerWindow();
            window.ForceReposition();
            return window;
        });

        internal InputBindingManager BindingManager => _bindingManager.Value;
        internal FileManager FileStore => _fileManager ?? throw new InvalidOperationException("FileManager not initialized");
        public static FOV FOVWindow => _fovWindow.Value;
        public static DetectedPlayerWindow DPWindow => _dpWindow.Value;
        public static GithubManager Github => _githubManager.Value;

        public double ActualFOV { get; set; } = 640;
        public string SystemSpecs { get; private set; } = "Loading system specs...";
        public Dictionary<int, string> ModelClasses { get; } = new();
        public bool CurrentModelIsDynamic { get; private set; }
        public Window? HostWindow => _hiddenHost;

        public async Task InitializeAsync()
        {
            Instance = this;

            SaveDictionary.EnsureDirectoriesExist();
            await LoadConfigurationsAsync();

            if (Directory.GetCurrentDirectory().Contains("Temp"))
            {
                MessageBox.Show(
                    "You are running EVADAV without extracting it from the zip file. " +
                    "Please extract EVADAV from the zip file or it will not run properly.",
                    "EVADAV");
            }

            DisplayManager.Initialize();
            InitializeWindows();
            EnsureRequiredFiles();

            _fileManager = new FileManager();
            FileManager.ModelLoaded -= OnModelLoaded;
            FileManager.ModelLoaded += OnModelLoaded;

            SetupKeybindings();
            PropertyChanger.ReceiveNewConfig = LoadConfig;
            ApplyInitialSettings();
            ListenForKeybinds();
            DisplayManager.DisplayChanged += OnDisplayChanged;

            AIManager.ClassesUpdated += classes =>
            {
                ModelClasses.Clear();
                foreach (var kvp in classes)
                    ModelClasses[kvp.Key] = kvp.Value;
            };
            AIManager.DynamicModelStatusChanged += dynamicModel => CurrentModelIsDynamic = dynamicModel;
            AIManager.ImageSizeUpdated += size =>
            {
                var fov = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
                if (fov > size)
                {
                    Dictionary.sliderSettings["FOV Size"] = size;
                    ActualFOV = size;
                    PropertyChanger.PostNewFOVSize(ActualFOV);
                }
            };

            _previousMouseMethod = AimSettings.MouseMovementMethod;
            ExtractMenuFont();
            LoadSpecsAsync();
        }

        public static string MenuFontPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "ui", "AtkinsonHyperlegible-Regular.ttf");
        public static string IconFontPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "ui", "FontAwesome6-Solid.otf");

        private static void ExtractMenuFont()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MenuFontPath)!);
                ExtractResource("pack://application:,,,/Graphics/Fonts/AtkinsonHyperlegible-Regular.ttf", MenuFontPath);
                ExtractResource("pack://application:,,,/Graphics/Fonts/Font Awesome 6 Free-Solid-900.otf", IconFontPath);
            }
            catch { }
        }

        private static void ExtractResource(string packUri, string dest)
        {
            var stream = Application.GetResourceStream(new Uri(packUri));
            if (stream?.Stream == null) return;
            using var input = stream.Stream;
            using var output = File.Create(dest);
            input.CopyTo(output);
        }

        public Window CreateHiddenHost()
        {
            _hiddenHost = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Opacity = 0,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize
            };
            _hiddenHost.Closing += (_, e) =>
            {
                if (Application.Current.ShutdownMode != ShutdownMode.OnExplicitShutdown)
                    return;
            };
            return _hiddenHost;
        }

        private void InitializeWindows()
        {
            var fov = FOVWindow;
            var dpw = DPWindow;
            fov.ForceReposition();
            dpw.ForceReposition();
            Dictionary.DetectedPlayerOverlay = dpw;
            Dictionary.FOVWindow = fov;
        }

        private void EnsureRequiredFiles()
        {
            var labelsPath = "bin\\labels\\labels.txt";
            var labelsDir = Path.GetDirectoryName(labelsPath);
            if (!string.IsNullOrEmpty(labelsDir) && !Directory.Exists(labelsDir))
                Directory.CreateDirectory(labelsDir);
            if (!File.Exists(labelsPath))
                File.WriteAllText(labelsPath, "Enemy");
        }

        private async Task LoadConfigurationsAsync()
        {
            await Task.Run(() =>
            {
                var configs = new[]
                {
                    (Dictionary.minimizeState, "bin\\minimize.cfg"),
                    (Dictionary.bindingSettings, "bin\\binding.cfg"),
                    (Dictionary.colorState, "bin\\colors.cfg"),
                    (Dictionary.filelocationState, "bin\\filelocations.cfg"),
                    (Dictionary.dropdownState, "bin\\dropdown.cfg"),
                    (Dictionary.toggleState, "bin\\toggles.cfg")
                };

                foreach (var (dict, path) in configs)
                    SaveDictionary.LoadJSON(dict, path);
            });

            LoadConfig();
            ApplyThemeColorFromConfig();
        }

        private void ApplyThemeColorFromConfig()
        {
            if (!Dictionary.colorState.TryGetValue("Theme Color", out var themeColor)) return;
            var colorString = themeColor?.ToString();
            if (string.IsNullOrEmpty(colorString)) return;
            try
            {
                if (colorString.Equals("#FF722ED1", StringComparison.OrdinalIgnoreCase) ||
                    colorString.Equals("#722ED1", StringComparison.OrdinalIgnoreCase) ||
                    colorString.Equals("#FF00C8B4", StringComparison.OrdinalIgnoreCase) ||
                    colorString.Equals("#00C8B4", StringComparison.OrdinalIgnoreCase))
                {
                    colorString = "#FF00D26A";
                    Dictionary.colorState["Theme Color"] = colorString;
                }
                ThemeManager.SetThemeColor(colorString);
                EVADAV.ImGuiUi.OstinStyle.SetAccent(ThemeManager.ThemeColor);
            }
            catch { }
        }

        private void SetupKeybindings()
        {
            var keybinds = new[]
            {
                "Aim Keybind", "Second Aim Keybind", "Dynamic FOV Keybind",
                "Emergency Stop Keybind", "Model Switch Keybind"
            };

            foreach (var keybind in keybinds)
                BindingManager.SetupDefault(keybind, Dictionary.bindingSettings[keybind].ToString());
        }

        private void ApplyInitialSettings()
        {
            ActualFOV = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
            PropertyChanger.PostNewFOVSize(ActualFOV);
            PropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["FOV Color"].ToString()));

            PropertyChanger.PostDPColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["Detected Player Color"].ToString()));
            PropertyChanger.PostDPFontSize((int)Convert.ToDouble(Dictionary.sliderSettings["AI Confidence Font Size"]));
            PropertyChanger.PostDPWCornerRadius((int)Convert.ToDouble(Dictionary.sliderSettings["Corner Radius"]));
            PropertyChanger.PostDPWBorderThickness(Convert.ToDouble(Dictionary.sliderSettings["Border Thickness"]));
            PropertyChanger.PostDPWOpacity(Convert.ToDouble(Dictionary.sliderSettings["Opacity"]));

            ApplyFovStyle();
            ApplyToggleSideEffects("FOV");
            ApplyToggleSideEffects("Show Detected Player");
            ApplyToggleSideEffects("Show AI Confidence");
            ApplyToggleSideEffects("StreamGuard");
            ApplyToggleSideEffects("EMA Smoothening");
        }

        private void LoadSpecsAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    var cpu = GetSpecs.GetSpecification("Win32_Processor", "Name");
                    var gpu = GetSpecs.GetSpecification("Win32_VideoController", "Name");
                    var mem = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
                    var ram = Math.Round(mem / (1024.0 * 1024.0), 0);
                    SystemSpecs = $"{cpu} • {gpu} • {ram}GB RAM";
                }
                catch
                {
                    SystemSpecs = "Specs unavailable";
                }
            });
        }

        private void ListenForKeybinds()
        {
            BindingManager.OnBindingPressed += id =>
            {
                if (id == "Model Switch Keybind") HandleModelSwitch();
                else if (id == "Dynamic FOV Keybind") ApplyDynamicFOV(true);
                else if (id == "Emergency Stop Keybind") HandleEmergencyStop();
            };
            BindingManager.OnBindingReleased += id =>
            {
                if (id == "Dynamic FOV Keybind") ApplyDynamicFOV(false);
            };
        }

        private void HandleModelSwitch()
        {
            if (!Dictionary.toggleState["Enable Model Switch Keybind"] || FileManager.CurrentlyLoadingModel)
                return;

            var models = FileStore.Models;
            if (models.Count == 0) return;

            int current = models.IndexOf(Dictionary.lastLoadedModel);
            int next = current >= 0 && current < models.Count - 1 ? current + 1 : 0;
            FileStore.SelectModel(models[next]);
        }

        private void ApplyDynamicFOV(bool apply)
        {
            RunOnUi(() =>
            {
                if (!Dictionary.toggleState["Dynamic FOV"])
                {
                    FOVWindow.Circle.BeginAnimation(FrameworkElement.WidthProperty, null);
                    FOVWindow.Circle.BeginAnimation(FrameworkElement.HeightProperty, null);
                    FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.WidthProperty, null);
                    FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.HeightProperty, null);
                    FOVWindow.UpdateFOVSize(ActualFOV);
                    return;
                }

                var targetSize = apply ? Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]) : ActualFOV;
                Dictionary.sliderSettings["FOV Size"] = targetSize;
                var duration = TimeSpan.FromMilliseconds(500);
                Animator.WidthShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualWidth, targetSize);
                Animator.HeightShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualHeight, targetSize);
                Animator.WidthShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualWidth, targetSize);
                Animator.HeightShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualHeight, targetSize);
            });
        }

        private void HandleEmergencyStop()
        {
            foreach (var feature in new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger" })
                Dictionary.toggleState[feature] = false;
            LogManager.Log(LogManager.LogLevel.Info, "[Emergency Stop Keybind] Disabled all AI features.", true);
        }

        public void ApplyToggleSideEffects(string title)
        {
            RunOnUi(() =>
            {
                switch (title)
                {
                    case "FOV":
                        FOVWindow.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Hidden;
                        if (Dictionary.toggleState[title]) FOVWindow.ForceReposition();
                        break;
                    case "Show Detected Player":
                        if (Dictionary.toggleState[title])
                        {
                            DPWindow.Show();
                            DPWindow.ForceReposition();
                        }
                        else
                        {
                            DPWindow.Hide();
                        }
                        DPWindow.DetectedPlayerFocus.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "Show AI Confidence":
                        DPWindow.DetectedPlayerConfidence.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "StreamGuard":
                        StreamGuardManager.ApplyStreamGuardToAllWindows(Dictionary.toggleState[title]);
                        break;
                    case "EMA Smoothening":
                        MouseManager.IsEMASmoothingEnabled = Dictionary.toggleState[title];
                        break;
                    case "Constant AI Tracking":
                        if (Dictionary.toggleState[title])
                        {
                            if (Dictionary.lastLoadedModel == "N/A")
                                Dictionary.toggleState[title] = false;
                            else
                                Dictionary.toggleState["Aim Assist"] = true;
                        }
                        break;
                    case "Aim Assist":
                        if (Dictionary.toggleState[title] && Dictionary.lastLoadedModel == "N/A")
                        {
                            Dictionary.toggleState[title] = false;
                            LogManager.Log(LogManager.LogLevel.Warning, "Please load a model first", true);
                        }
                        break;
                }
            });
        }

        public void ApplyFovStyle()
        {
            RunOnUi(() =>
            {
                string style = Dictionary.dropdownState.TryGetValue("FOV Style", out var raw)
                ? raw?.ToString() ?? "Circle"
                : "Circle";
            bool circle = style != "Rectangle";
                FOVWindow.Circle.Visibility = circle ? Visibility.Visible : Visibility.Collapsed;
                FOVWindow.RectangleShape.Visibility = circle ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        public void ApplyFovColor(float r, float g, float b, float a)
        {
            var color = Color.FromArgb(
                (byte)Math.Clamp(a * 255, 0, 255),
                (byte)Math.Clamp(r * 255, 0, 255),
                (byte)Math.Clamp(g * 255, 0, 255),
                (byte)Math.Clamp(b * 255, 0, 255));
            Dictionary.colorState["FOV Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            RunOnUi(() => PropertyChanger.PostColor(color));
        }

        public void ApplyEspColor(float r, float g, float b, float a)
        {
            var color = Color.FromArgb(
                (byte)Math.Clamp(a * 255, 0, 255),
                (byte)Math.Clamp(r * 255, 0, 255),
                (byte)Math.Clamp(g * 255, 0, 255),
                (byte)Math.Clamp(b * 255, 0, 255));
            Dictionary.colorState["Detected Player Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            RunOnUi(() => PropertyChanger.PostDPColor(color));
        }

        public void ApplyThemeColor(float r, float g, float b)
        {
            var color = Color.FromRgb(
                (byte)Math.Clamp(r * 255, 0, 255),
                (byte)Math.Clamp(g * 255, 0, 255),
                (byte)Math.Clamp(b * 255, 0, 255));
            Dictionary.colorState["Theme Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            EVADAV.ImGuiUi.OstinStyle.SetAccent(r, g, b);
            RunOnUi(() => ThemeManager.SetThemeColor(color));
        }

        public async Task OnMouseMethodChanged(string method)
        {
            if (method == _previousMouseMethod) return;
            _previousMouseMethod = method;

            await RunOnUiAsync(async () =>
            {
                if (method != ArduinoMouse.MethodName)
                    ArduinoMouse.Close();

                bool ok = true;
                if (method == "LG HUB")
                    ok = new LGHubMain().Load();
                else if (method == "Razer Synapse (Require Razer Peripheral)")
                    ok = await RZMouse.Load();
                else if (method == "ddxoft Virtual Input Driver")
                    ok = await DdxoftMain.Load();
                else if (method == ArduinoMouse.MethodName)
                    ok = await ArduinoMouse.Load();

                if (!ok)
                {
                    Dictionary.dropdownState["Mouse Movement Method"] = "Mouse Event";
                    _previousMouseMethod = "Mouse Event";
                    ArduinoMouse.Close();
                }
            });
        }

        public async Task ReloadArduino()
        {
            if (AimSettings.IsArduinoMouse)
                await ArduinoMouse.Load();
        }

        public void TestArduino() => ArduinoMouse.TestJump();

        public void OnFovSizeChanged(double value)
        {
            ActualFOV = value;
            RunOnUi(() => PropertyChanger.PostNewFOVSize(ActualFOV));
        }

        public void OnDynamicFovSizeChanged(double value)
        {
            if (Dictionary.toggleState["Dynamic FOV"])
                RunOnUi(() => PropertyChanger.PostNewFOVSize(value));
        }

        public void OnEmaChanged(double value)
        {
            if (Dictionary.toggleState["EMA Smoothening"])
                MouseManager.smoothingFactor = value;
        }

        public static void ApplyConfigLoadDefaults(IDictionary<string, dynamic> sliderSettings)
        {
            sliderSettings["AI FPS Limit"] = 0;
        }

        public void LoadConfig(string path = "bin\\configs\\Default.cfg", bool loadingFromConfigList = false)
        {
            ApplyConfigLoadDefaults(Dictionary.sliderSettings);
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, path);
            SaveDictionary.LoadJSON(Dictionary.dropdownState, path);

            if (Dictionary.sliderSettings.TryGetValue("FOV Size", out var fov))
            {
                ActualFOV = Convert.ToDouble(fov);
                RunOnUi(() => PropertyChanger.PostNewFOVSize(ActualFOV));
            }

            if (!loadingFromConfigList) return;

            if (Dictionary.sliderSettings.TryGetValue("Suggested Model", out var model))
            {
                var suggestedModel = model?.ToString() ?? "N/A";
                if (suggestedModel != "N/A" && !string.IsNullOrEmpty(suggestedModel))
                {
                    RunOnUi(() => MessageBox.Show(
                        $"The creator of this model suggests you use this model:\n{suggestedModel}",
                        "Suggested Model - EVADAV"));
                }
            }
        }

        public void SaveNamedConfig(string name, string suggestedModel)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            SaveDictionary.WriteJSON(
                Dictionary.sliderSettings
                    .Concat(Dictionary.dropdownState)
                    .GroupBy(kvp => kvp.Key)
                    .ToDictionary(g => g.Key, g => g.First().Value),
                $"bin\\configs\\{name}.cfg",
                suggestedModel);
            LogManager.Log(LogManager.LogLevel.Info, "Config has been saved to bin/configs.", true);
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            DisplayManager.ForceUpdateWindows();
        }

        private void OnModelLoaded(AIManager manager)
        {
            RunOnUi(() =>
            {
                if (!PerformanceHelperState.ShouldPrompt()) return;
                ShowPerformanceHelper(manager, PerformanceHelperWindow.LaunchMode.CompactPrompt);
            });
        }

        public void ShowPerformanceHelper()
        {
            RunOnUi(() =>
            {
                var manager = FileManager.AIManager;
                if (manager == null || !manager.IsLoaded)
                {
                    LogManager.Log(LogManager.LogLevel.Warning, "Load a model before opening the Performance Helper.", true, 3000);
                    return;
                }
                ShowPerformanceHelper(manager, PerformanceHelperWindow.LaunchMode.FullHelper);
            });
        }

        private void ShowPerformanceHelper(AIManager manager, PerformanceHelperWindow.LaunchMode launchMode)
        {
            if (_performanceHelperOpen ||
                !ReferenceEquals(FileManager.AIManager, manager) ||
                !manager.IsLoaded)
            {
                return;
            }

            _performanceHelperOpen = true;
            try
            {
                var helper = new PerformanceHelperWindow(this, manager, launchMode)
                {
                    Owner = _hiddenHost
                };
                helper.ShowDialog();
            }
            finally
            {
                _performanceHelperOpen = false;
            }
        }

        internal async Task<bool> ChangeImageSizeAsync(string newSize)
        {
            if (string.IsNullOrWhiteSpace(newSize)) return false;
            newSize = newSize.Trim();
            if (!int.TryParse(newSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedSize))
                return false;

            await FileManager.ModelOperationLock.WaitAsync();
            FileManager.CurrentlyLoadingModel = true;
            string? previousSize = null;
            string? loadedModel = null;
            string? modelPath = null;

            try
            {
                if (FileManager.AIManager == null || Dictionary.lastLoadedModel == "N/A")
                {
                    Dictionary.dropdownState["Image Size"] = newSize;
                    LogManager.Log(LogManager.LogLevel.Info, $"Image size set to {newSize}x{newSize} (no model loaded)", true, 2000);
                    return true;
                }

                previousSize = Dictionary.dropdownState["Image Size"];
                loadedModel = Dictionary.lastLoadedModel;
                modelPath = Path.Combine("bin/models", loadedModel);
                AIManager managerToReplace = FileManager.AIManager;
                LogManager.Log(LogManager.LogLevel.Info, $"Image size changing to {newSize}");

                managerToReplace.RequestSizeChange(requestedSize);
                await Task.Delay(100);

                FileManager.AIManager = null;
                managerToReplace.Dispose();

                Dictionary.dropdownState["Image Size"] = newSize;

                var manager = new AIManager(modelPath);
                bool loaded = await manager.InitializationTask;

                if (loaded)
                {
                    FileManager.AIManager = manager;
                    Dictionary.lastLoadedModel = loadedModel;
                    string actualSize = AimSettings.ImageSize.ToString(CultureInfo.InvariantCulture);
                    Dictionary.dropdownState["Image Size"] = actualSize;
                    if (actualSize == newSize)
                    {
                        LogManager.Log(LogManager.LogLevel.Info, $"Successfully changed image size to {actualSize}x{actualSize}", true, 2000);
                        return true;
                    }

                    LogManager.Log(LogManager.LogLevel.Warning,
                        $"Model loaded at {actualSize}x{actualSize}; requested {newSize}x{newSize} was not applied.",
                        true, 5000);
                    return false;
                }

                manager.Dispose();
                FileManager.AIManager = null;
                Dictionary.dropdownState["Image Size"] = previousSize;
                LogManager.Log(LogManager.LogLevel.Error, $"Model could not reload at {newSize}x{newSize}. Restoring {previousSize}x{previousSize}.", true, 5000);

                var restoredManager = new AIManager(modelPath);
                if (await restoredManager.InitializationTask)
                {
                    FileManager.AIManager = restoredManager;
                    Dictionary.lastLoadedModel = loadedModel;
                    Dictionary.dropdownState["Image Size"] = AimSettings.ImageSize.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    restoredManager.Dispose();
                    FileManager.AIManager = null;
                    Dictionary.lastLoadedModel = "N/A";
                }
                return false;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(previousSize))
                    Dictionary.dropdownState["Image Size"] = previousSize;
                LogManager.Log(LogManager.LogLevel.Error, $"Error changing image size: {ex.Message}", true, 5000);
                return false;
            }
            finally
            {
                FileManager.CurrentlyLoadingModel = false;
                FileManager.ModelOperationLock.Release();
            }
        }

        internal async Task<bool> ApplyPerformanceRecommendationAsync(PerformanceRecommendation recommendation)
        {
            if (recommendation.CanChangeImageSize &&
                recommendation.SuggestedImageSize != AimSettings.ImageSize)
            {
                bool changed = await ChangeImageSizeAsync(recommendation.SuggestedImageSize.ToString());
                if (!changed) return false;
            }

            Dictionary.sliderSettings["AI FPS Limit"] = recommendation.SuggestedFpsLimit;
            return true;
        }

        public void OpenFolder(string relative)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "bin", relative);
            if (Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
            else
                LogManager.Log(LogManager.LogLevel.Warning, $"Directory not found: {path}", true);
        }

        public async Task CheckForUpdates()
        {
            try
            {
                var updateManager = new UpdateManager();
                await updateManager.CheckForUpdate("v2.5.0");
                updateManager.Dispose();
            }
            catch { }
        }

        public void PickDdxoftDll()
        {
            RunOnUi(() =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };
                if (dialog.ShowDialog() == true)
                    Dictionary.filelocationState["ddxoft DLL Location"] = dialog.FileName;
            });
        }

        public void Shutdown()
        {
            RunOnUi(() =>
            {
                if (_fileManager != null)
                    _fileManager.InQuittingState = true;

                Dictionary.toggleState["Aim Assist"] = false;
                Dictionary.toggleState["FOV"] = false;
                Dictionary.toggleState["Show Detected Player"] = false;

                try { FOVWindow.Close(); } catch { }
                try { DPWindow.Close(); } catch { }

                ArduinoMouse.Close();
                if (Dictionary.dropdownState.TryGetValue("Mouse Movement Method", out var method) &&
                    method?.ToString() == "LG HUB")
                {
                    LGMouse.Close();
                }

                Dictionary.colorState["Theme Color"] = ThemeManager.GetThemeColorHex();
                SaveDictionary.WriteJSON(Dictionary.sliderSettings
                    .Concat(Dictionary.dropdownState)
                    .GroupBy(kvp => kvp.Key)
                    .ToDictionary(g => g.Key, g => g.First().Value));
                SaveDictionary.WriteJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
                SaveDictionary.WriteJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
                SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
                SaveDictionary.WriteJSON(Dictionary.colorState, "bin\\colors.cfg");
                SaveDictionary.WriteJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
                SaveDictionary.WriteJSON(Dictionary.toggleState, "bin\\toggles.cfg");

                FileManager.AIManager?.Dispose();
                DisplayManager.DisplayChanged -= OnDisplayChanged;
                DisplayManager.Dispose();
                Application.Current.Shutdown();
            });
        }

        private void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        private Task RunOnUiAsync(Func<Task> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return action();
            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }
}
