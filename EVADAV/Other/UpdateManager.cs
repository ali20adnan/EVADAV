using EVADAV.Other;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Other
{
    internal class UpdateManager
    {
        private readonly HttpClient client;

        public UpdateManager()
        {
            client = new HttpClient();
        }

        private int CompareVersions(string currentVersion, string latestVersion)
        {
            try
            {
                // Remove 'v' prefix if present
                currentVersion = currentVersion.TrimStart('v', 'V');
                latestVersion = latestVersion.TrimStart('v', 'V');

                // Parse versions
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);

                return current.CompareTo(latest);
            }
            catch (Exception ex)
            {

                // Fallback to string comparison if parsing fails
                return string.Compare(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
            }
        }

        public async Task CheckForUpdate(string currentVersion)
        {
            LogManager.Log(LogManager.LogLevel.Info, "EVADAV does not use remote updates.", true);
            await Task.CompletedTask;
        }

        private async Task DoUpdate(string latestZipUrl)
        {
            string envTempPath = Path.GetTempPath();
            string localZipPath = Path.Combine(envTempPath, "EVADAVUpdate.zip");

            var response = await client.GetAsync(new Uri(latestZipUrl), HttpCompletionOption.ResponseHeadersRead);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await stream.CopyToAsync(fileStream);

            // Extract update to %temp%
            string extractPath = Path.Combine(envTempPath, "EVADAVUpdate");
            await Task.Run(() => // Run extraction in a separate task
            {
                ZipFile.ExtractToDirectory(localZipPath, extractPath, true);
            });

            // Create a batch script to move the files and restart EVADAV
            string? mainAppPath = Environment.ProcessPath;

            string? mainAppDir = Path.GetDirectoryName(mainAppPath) ?? throw new InvalidOperationException("Failed to get the directory name from the main module file path.");

            string batchScriptPath = Path.Combine(mainAppDir!, "update.bat");

            using (StreamWriter sw = new(batchScriptPath))
            {
                sw.WriteLine("@echo off");
                sw.WriteLine("timeout /t 3 /nobreak");
                sw.WriteLine($"xcopy /Y \"{extractPath}\\*\" \"{mainAppDir}\"");
                sw.WriteLine($"start \"\" \"{mainAppPath}\"");
                sw.WriteLine($"del /f \"{localZipPath}\"");
                sw.WriteLine($"rd /s /q \"{extractPath}\"");
                sw.WriteLine($"del /f \"{batchScriptPath}\"");
                sw.WriteLine($"( del /F /Q \"%~f0\" >nul 2>&1 & exit ) >nul");
            }

            Process.Start(batchScriptPath);
            Environment.Exit(0);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}