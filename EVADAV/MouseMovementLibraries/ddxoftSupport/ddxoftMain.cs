using Other;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace MouseMovementLibraries.ddxoftSupport
{
    internal class DdxoftMain
    {
        public static ddxoftMouse ddxoftInstance = new();
        private static readonly string ddxoftpath = "ddxoft.dll";

        public static async Task<bool> DLLLoading()
        {
            try
            {
                if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) == false)
                {
                    MessageBox.Show("The ddxoft Virtual Input Driver requires EVADAV to be run as an administrator, please close EVADAV and run it as administrator to use this movement method.", "EVADAV");
                    return false;
                }

                if (!File.Exists(ddxoftpath))
                {
                    LogManager.Log(LogManager.LogLevel.Error, "ddxoft.dll is missing. Place it next to EVADAV.exe to use this movement method.", true);
                    return false;
                }

                if (ddxoftInstance.Load(ddxoftpath) != 1 || ddxoftInstance.btn!(0) != 1)
                {
                    MessageBox.Show("The ddxoft virtual input driver is not compatible with your PC, please try a different Mouse Movement Method.", "EVADAV");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load ddxoft virtual input driver.\n\n" + ex.ToString(), "EVADAV");
                return false;
            }
        }

        public static async Task<bool> Load() => await DLLLoading();
    }
}