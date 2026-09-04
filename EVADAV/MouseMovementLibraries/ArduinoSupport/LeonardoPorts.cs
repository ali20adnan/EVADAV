using System.IO.Ports;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MouseMovementLibraries.ArduinoSupport
{
    internal sealed class LeonardoPortInfo
    {
        public string Port { get; init; } = "";
        public string Description { get; init; } = "";
        public bool IsLeonardo { get; init; }
        public string VidPid { get; init; } = "";
        public string Label => IsLeonardo
            ? $"{Port} — Arduino Leonardo"
            : string.IsNullOrWhiteSpace(Description) ? Port : $"{Port} — {Description}";
    }

    internal static class LeonardoPorts
    {
        public const string MethodName = "Arduino Leonardo (Host Shield)";

        private static readonly (string VidPid, string Name)[] LeonardoIds =
        {
            ("VID_2341&PID_8036", "Arduino Leonardo"),
            ("VID_2A03&PID_8036", "Arduino.org Leonardo"),
            ("VID_2341&PID_8037", "Arduino Micro (32U4)"),
            ("VID_2A03&PID_8037", "Arduino.org Micro"),
        };

        public static List<LeonardoPortInfo> List()
        {
            var byPort = new Dictionary<string, LeonardoPortInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var name in SerialPort.GetPortNames())
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    byPort[name] = new LeonardoPortInfo { Port = name, Description = name };
                }
            }
            catch { /* ignore */ }

            ScanUsbEnum(byPort);

            return byPort.Values
                .OrderByDescending(p => p.IsLeonardo)
                .ThenBy(p => PadCom(p.Port), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> AutoTryOrder(string preferred)
        {
            var all = List();
            var ordered = new List<string>();
            void Add(string? p)
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                if (p.Equals("AUTO", StringComparison.OrdinalIgnoreCase)) return;
                if (p.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return;
                if (!ordered.Contains(p, StringComparer.OrdinalIgnoreCase))
                    ordered.Add(p);
            }

            Add(preferred);
            foreach (var p in all.Where(x => x.IsLeonardo))
                Add(p.Port);

            foreach (var p in all)
            {
                if (p.VidPid.Contains("VID_2341", StringComparison.OrdinalIgnoreCase)
                    || p.VidPid.Contains("VID_2A03", StringComparison.OrdinalIgnoreCase)
                    || p.VidPid.Contains("PID_8036", StringComparison.OrdinalIgnoreCase)
                    || p.VidPid.Contains("PID_8037", StringComparison.OrdinalIgnoreCase))
                    Add(p.Port);
            }

            if (ordered.Count == 0)
            {
                foreach (var p in all)
                {
                    if (p.Port.Equals("COM1", StringComparison.OrdinalIgnoreCase)) continue;
                    Add(p.Port);
                }
            }

            return ordered;
        }

        private static void ScanUsbEnum(Dictionary<string, LeonardoPortInfo> byPort)
        {
            try
            {
                using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
                if (usb == null) return;
                foreach (var vidPid in usb.GetSubKeyNames())
                {
                    var match = MatchLeonardo(vidPid);
                    using var vp = usb.OpenSubKey(vidPid);
                    if (vp == null) continue;
                    foreach (var inst in vp.GetSubKeyNames())
                    {
                        using var instKey = vp.OpenSubKey(inst);
                        if (instKey == null) continue;
                        var friendly = instKey.GetValue("FriendlyName") as string
                                       ?? instKey.GetValue("DeviceDesc") as string
                                       ?? "";
                        string? port = null;
                        using (var dp = instKey.OpenSubKey("Device Parameters"))
                            port = dp?.GetValue("PortName") as string;

                        if (string.IsNullOrWhiteSpace(port))
                            port = PortFromFriendly(friendly);
                        if (string.IsNullOrWhiteSpace(port)) continue;

                        bool leo = match != null
                                   || friendly.Contains("Leonardo", StringComparison.OrdinalIgnoreCase)
                                   || friendly.Contains("ATmega32U4", StringComparison.OrdinalIgnoreCase);

                        byPort[port] = new LeonardoPortInfo
                        {
                            Port = port,
                            Description = string.IsNullOrWhiteSpace(friendly) ? (match ?? port) : friendly,
                            IsLeonardo = leo,
                            VidPid = vidPid,
                        };
                    }
                }
            }
            catch { /* registry may be restricted */ }
        }

        private static string? MatchLeonardo(string vidPid)
        {
            foreach (var id in LeonardoIds)
            {
                if (vidPid.Contains(id.VidPid, StringComparison.OrdinalIgnoreCase))
                    return id.Name;
            }
            return null;
        }

        private static string? PortFromFriendly(string friendly)
        {
            var m = Regex.Match(friendly ?? "", @"\((COM\d+)\)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string PadCom(string port)
        {
            var m = Regex.Match(port ?? "", @"COM(\d+)", RegexOptions.IgnoreCase);
            return m.Success ? $"COM{m.Groups[1].Value.PadLeft(3, '0')}" : port ?? "";
        }
    }
}
