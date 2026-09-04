using EVADAV;
using EVADAV.AILogic;
using EVADAV.Class;
using Class;
using System.IO;
using System.Windows;

namespace Other
{
    internal class FileManager
    {
        public FileSystemWatcher? ModelFileWatcher;
        public FileSystemWatcher? ConfigFileWatcher;

        public readonly List<string> Models = new();
        public readonly List<string> Configs = new();

        public string LoadedModelLabel { get; private set; } = "Loaded Model: N/A";
        public string LoadedConfigLabel { get; private set; } = "Loaded Config: N/A";

        public event Action? ListsChanged;

        public bool InQuittingState = false;

        public static AIManager? AIManager;
        public static event Action<AIManager>? ModelLoaded;
        internal static readonly SemaphoreSlim ModelOperationLock = new(1, 1);
        public static bool CurrentlyLoadingModel = false;

        public FileManager()
        {
            CheckForRequiredFolders();
            InitializeFileWatchers();
            LoadModelsIntoListBox(null, null);
            LoadConfigsIntoListBox(null, null);
        }

        private void CheckForRequiredFolders()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = ["bin\\models", "bin\\images", "bin\\labels", "bin\\configs"];

            try
            {
                foreach (string dir in dirs)
                {
                    string fullPath = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating a required directory: {ex}");
                Application.Current.Shutdown();
            }
        }

        public void SelectModel(string selectedModel)
        {
            _ = SelectModelAsync(selectedModel);
        }

        public async Task SelectModelAsync(string selectedModel)
        {
            if (string.IsNullOrWhiteSpace(selectedModel)) return;

            string modelPath = Path.Combine("bin/models", selectedModel);

            if (Dictionary.lastLoadedModel == selectedModel || !ModelOperationLock.Wait(0))
                return;

            CurrentlyLoadingModel = true;
            string previousModel = Dictionary.lastLoadedModel;
            AIManager? previousManager = AIManager;
            AIManager? manager = null;
            AIManager? loadedManager = null;
            bool notifyModelLoaded = false;

            var toggleKeys = new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger", "Show Detected Player", "Show AI Confidence", "Show Tracers" };
            var originalToggleStates = new Dictionary<string, dynamic>();

            try
            {
                foreach (var key in toggleKeys)
                {
                    if (Dictionary.toggleState.TryGetValue(key, out dynamic? originalValue))
                    {
                        originalToggleStates[key] = originalValue;
                    }

                    Dictionary.toggleState[key] = false;
                }

                await Task.Delay(150);

                DisposeManager(previousManager, "Previous model session did not stop cleanly");
                AIManager = null;

                manager = await CreateLoadedManagerAsync(modelPath);
                if (manager == null)
                {
                    bool restored = await RestorePreviousModelAsync(previousModel);
                    if (!restored)
                    {
                        MarkNoModelLoaded();
                    }

                    LogManager.Log(LogManager.LogLevel.Error, $"Failed to load model: {selectedModel}", true, 5000);
                    return;
                }

                AIManager = manager;
                loadedManager = manager;
                manager = null;
                Dictionary.lastLoadedModel = selectedModel;

                LoadedModelLabel = "Loaded Model: " + selectedModel;
                LogManager.Log(LogManager.LogLevel.Info, LoadedModelLabel, true, 2000);
                notifyModelLoaded = true;
            }
            catch (Exception ex)
            {
                DisposeManager(manager, "Failed model session did not stop cleanly");
                bool restored = await RestorePreviousModelAsync(previousModel);
                if (!restored)
                {
                    MarkNoModelLoaded();
                }

                LogManager.Log(LogManager.LogLevel.Error, $"Failed to load model: {selectedModel}. {ex.Message}", true, 5000);
            }
            finally
            {
                foreach (var keyValuePair in originalToggleStates)
                {
                    Dictionary.toggleState[keyValuePair.Key] = keyValuePair.Value;
                }

                CurrentlyLoadingModel = false;
                ModelOperationLock.Release();
                NotifyListsChanged();
            }

            if (notifyModelLoaded && loadedManager != null)
            {
                try
                {
                    ModelLoaded?.Invoke(loadedManager);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Warning, $"Model loaded, but a load notification failed: {ex.Message}", true, 5000);
                }
            }
        }

        private static async Task<AIManager?> CreateLoadedManagerAsync(string modelPath)
        {
            var manager = new AIManager(modelPath);
            if (await manager.InitializationTask)
            {
                return manager;
            }

            manager.Dispose();
            return null;
        }

        private async Task<bool> RestorePreviousModelAsync(string previousModel)
        {
            if (previousModel == "N/A")
                return false;

            string previousModelPath = Path.Combine("bin/models", previousModel);
            if (!File.Exists(previousModelPath))
                return false;

            AIManager? restoredManager = await CreateLoadedManagerAsync(previousModelPath);
            if (restoredManager == null)
                return false;

            AIManager = restoredManager;
            Dictionary.lastLoadedModel = previousModel;
            LoadedModelLabel = $"Loaded Model: {previousModel}";
            return true;
        }

        private void MarkNoModelLoaded()
        {
            AIManager = null;
            Dictionary.lastLoadedModel = "N/A";
            LoadedModelLabel = "Loaded Model: N/A";
        }

        private static void DisposeManager(AIManager? manager, string warningPrefix)
        {
            try
            {
                manager?.Dispose();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warning, $"{warningPrefix}: {ex.Message}");
            }
        }

        public void SelectConfig(string selectedConfig)
        {
            if (string.IsNullOrWhiteSpace(selectedConfig)) return;

            string configPath = Path.Combine("bin/configs", selectedConfig);
            Dictionary.lastLoadedConfig = selectedConfig;

            AppController.ApplyConfigLoadDefaults(Dictionary.sliderSettings);
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, configPath);
            PropertyChanger.PostNewConfig(configPath, true);

            LoadedConfigLabel = "Loaded Config: " + selectedConfig;
            NotifyListsChanged();
        }

        public void InitializeFileWatchers()
        {
            ModelFileWatcher = new FileSystemWatcher();
            ConfigFileWatcher = new FileSystemWatcher();

            InitializeWatcher(ref ModelFileWatcher, "bin/models", "*.onnx");
            InitializeWatcher(ref ConfigFileWatcher, "bin/configs", "*.cfg");
        }

        private void InitializeWatcher(ref FileSystemWatcher watcher, string path, string filter)
        {
            watcher.Path = path;
            watcher.Filter = filter;
            watcher.EnableRaisingEvents = true;

            if (filter == "*.onnx")
            {
                watcher.Changed += LoadModelsIntoListBox;
                watcher.Created += LoadModelsIntoListBox;
                watcher.Deleted += LoadModelsIntoListBox;
                watcher.Renamed += LoadModelsIntoListBox;
            }
            else if (filter == "*.cfg")
            {
                watcher.Changed += LoadConfigsIntoListBox;
                watcher.Created += LoadConfigsIntoListBox;
                watcher.Deleted += LoadConfigsIntoListBox;
                watcher.Renamed += LoadConfigsIntoListBox;
            }
        }

        public void LoadModelsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (InQuittingState) return;

            void Refresh()
            {
                string[] onnxFiles = Directory.Exists("bin/models")
                    ? Directory.GetFiles("bin/models", "*.onnx")
                    : [];

                Models.Clear();
                foreach (string filePath in onnxFiles)
                {
                    Models.Add(Path.GetFileName(filePath));
                }

                string? lastLoadedModel = Dictionary.lastLoadedModel;
                LoadedModelLabel = $"Loaded Model: {lastLoadedModel}";
                NotifyListsChanged();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(Refresh);
            else
                Refresh();
        }

        public void LoadConfigsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (InQuittingState) return;

            void Refresh()
            {
                string[] configFiles = Directory.Exists("bin/configs")
                    ? Directory.GetFiles("bin/configs", "*.cfg")
                    : [];

                Configs.Clear();
                foreach (string filePath in configFiles)
                {
                    Configs.Add(Path.GetFileName(filePath));
                }

                string? lastLoadedConfig = Dictionary.lastLoadedConfig;
                LoadedConfigLabel = "Loaded Config: " + lastLoadedConfig;
                NotifyListsChanged();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(Refresh);
            else
                Refresh();
        }

        private void NotifyListsChanged()
        {
            ListsChanged?.Invoke();
        }

        public static Task<HashSet<string>> RetrieveAndAddFiles(string repoLink, string localPath, HashSet<string> allFiles)
        {
            return Task.FromResult(allFiles);
        }
    }
}
