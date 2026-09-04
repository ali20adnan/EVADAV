using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.RazerSupport
{
    internal class RZMouse
    {
        #region Razer Variables

        private const string rzctlpath = "rzctl.dll";

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool init();

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_move(int x, int y, bool starting_point);

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_click(int up_down);

        private static readonly List<string> Razer_HID = [];

        #endregion
        public static async Task<bool> Load()
        {
            if (!await EnsureRazerSynapseInstalled())
                return false;

            if (!File.Exists(rzctlpath))
            {
                new NoticeBar("rzctl.dll is missing. Place it next to EVADAV.exe to use Razer mouse movement.", 6000).Show();
                return false;
            }

            if (!DetectRazerDevices())
            {
                new NoticeBar("No Razer device detected. This method is unusable.", 5000).Show();
                return false;
            }
            try
            {
                return init();
            }
            catch (BadImageFormatException)
            {
                new NoticeBar("rzctl.dll is incompatible. Use a matching local copy.", 5000).Show();
                return false;
            }
            // Hopefully this method will solve the issue for the users who have/don't have the drivers
            // If the user gets this error, Failed to initialize Razer mode, then it will attempt to download the Release version.
            // And if that still doesn't work, then it will error again, which in this case would mean they don't have the driver for vs &&|| vc 2015–2022
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Razer mode.\n{ex.Message}",
                                "EVADAV", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            /* Commenting this method out to replace with a more enhanced version
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Razer mode.\n{ex.Message}",
                        "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            */
        }
        #region Razer Synapse & Device Checks

        private static bool DetectRazerDevices()
        {
            Razer_HID.Clear();
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE 'Razer%'");
            var devices = searcher.Get().Cast<ManagementBaseObject>();

            Razer_HID.AddRange(devices.Select(d => d["DeviceID"]?.ToString() ?? string.Empty));
            return Razer_HID.Count > 0;
        }

        private static async Task<bool> EnsureRazerSynapseInstalled()
        {
            if (Process.GetProcessesByName("RazerAppEngine").Any())
                return true;

            var response = MessageBox.Show("Razer Synapse is not running. Do you have it installed?",
                                           "EVADAV - Razer Synapse", MessageBoxButton.YesNo);
            if (response == MessageBoxResult.No)
            {
                await DownloadAndInstallRazerSynapse();
                return false;
            }

            if (!IsRazerSynapseInstalled())
            {
                var install = MessageBox.Show("Razer Synapse is not installed. Would you like to install it?",
                                              "EVADAV - Razer Synapse", MessageBoxButton.YesNo);
                if (install == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallRazerSynapse();
                    return false;
                }
                return false;
            }

            return true;
        }

        private static bool IsRazerSynapseInstalled()
        {
            return Directory.Exists(@"C:\Program Files\Razer") ||
                   Directory.Exists(@"C:\Program Files (x86)\Razer") ||
                   Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Razer") != null;
        }

        private static async Task DownloadAndInstallRazerSynapse()
        {
            try
            {
                using HttpClient client = new();
                var response = await client.GetAsync("https://rzr.to/synapse-new-pc-download-beta");
                if (!response.IsSuccessStatusCode)
                {
                    new NoticeBar("Failed to download Razer Synapse installer.", 4000).Show();
                    return;
                }

                string path = Path.Combine(Path.GetTempPath(), "rz.exe");
                var content = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(path, content);

                Process.Start(new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C start rz.exe",
                    WorkingDirectory = Path.GetTempPath()
                });

                new NoticeBar("Razer Synapse downloaded. Please confirm the UAC prompt to install.", 4000).Show();
            }
            catch
            {
                new NoticeBar("Error occurred while downloading Synapse.", 4000).Show();
            }
        }

        #endregion

        #region Visual Studio & Redist Checks

        private static bool IsVcRedistInstalled()
        {
            string[] keys =
            {
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                @"SOFTWARE\Microsoft\VisualStudio\17.0\VC\Runtimes\x64"
            };

            foreach (string path in keys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key != null && Convert.ToInt32(key.GetValue("Installed", 0)) == 1)
                    return true;
            }

            return false;
        }

        private static bool IsVisualStudioInstalled()
        {
            string[] uninstallRoots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string root in uninstallRoots)
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key == null) continue;

                foreach (string subKey in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(subKey);
                    string name = sub?.GetValue("DisplayName") as string ?? "";

                    if (name.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        #endregion
    }
}
