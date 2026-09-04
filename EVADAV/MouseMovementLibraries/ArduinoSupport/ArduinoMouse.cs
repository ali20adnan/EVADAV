using System.IO.Ports;
using System.Security.Principal;
using System.Text;
using EVADAV.Class;
using Other;

namespace MouseMovementLibraries.ArduinoSupport
{
    internal static class ArduinoMouse
    {
        public const string MethodName = LeonardoPorts.MethodName;

        private const int Baud = 115200;

        private static readonly object Gate = new();
        private static SerialPort? _port;
        private static string _activePort = "";
        private static bool _hostShield;
        private static bool _sawMouse;
        private static int _reconnectBusy;

        public static bool IsReady
        {
            get
            {
                lock (Gate)
                    return PortOpen();
            }
        }

        public static string Status { get; private set; } = "Leonardo disconnected";

        public static bool IsAdministrator()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static async Task<bool> Load()
        {
            if (!IsAdministrator())
            {
                Status = "Arduino mouse method requires EVADAV as Administrator. Right-click EVADAV.exe → Run as administrator.";
                LogManager.Log(LogManager.LogLevel.Error, Status, true, 6000);
                return false;
            }

            var preferred = AimSettings.ArduinoComPort;
            var ok = await Task.Run(() =>
            {
                lock (Gate)
                    return OpenLocked(preferred);
            }).ConfigureAwait(true);

            LogManager.Log(ok ? LogManager.LogLevel.Info : LogManager.LogLevel.Error, Status, true, 5000);
            return ok;
        }

        public static void Move(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            dx = Math.Clamp(dx, -127, 127);
            dy = Math.Clamp(dy, -127, 127);
            WriteLine($"{dx},{dy}");
        }

        public static void MouseDown() => WriteLine("LDOWN");
        public static void MouseUp() => WriteLine("LUP");
        public static void Click() => WriteLine("CLICK");

        public static void TestJump()
        {
            if (!IsAdministrator())
            {
                LogManager.Log(LogManager.LogLevel.Error,
                    "Arduino TEST needs Administrator. Restart EVADAV as admin.", true, 5000);
                return;
            }

            if (!WriteLine("TEST"))
            {
                LogManager.Log(LogManager.LogLevel.Warning, Status, true, 4000);
                return;
            }
            LogManager.Log(LogManager.LogLevel.Info, "Arduino TEST sent — cursor should jump ~120px.", true, 3000);
        }

        public static void Close()
        {
            lock (Gate)
            {
                ClosePortLocked();
                Status = "Leonardo disconnected";
            }
        }

        private static bool WriteLine(string line)
        {
            lock (Gate)
            {
                if (!PortOpen())
                {
                    KickReconnectLocked();
                    return false;
                }

                try
                {
                    var bytes = Encoding.ASCII.GetBytes(line + "\n");
                    _port!.BaseStream.Write(bytes, 0, bytes.Length);
                    _port.BaseStream.Flush();
                    return true;
                }
                catch (TimeoutException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Status = $"Leonardo write error on {_activePort}: {ex.Message}";
                    ClosePortLocked();
                    KickReconnectLocked();
                    return false;
                }
            }
        }

        private static bool OpenLocked(string preferred)
        {
            if (PortOpen() && PortMatchesPreference(_activePort, preferred))
                return true;

            ClosePortLocked();
            var devices = LeonardoPorts.List();
            var tryList = LeonardoPorts.AutoTryOrder(preferred);

            if (tryList.Count == 0)
            {
                var listed = devices.Count == 0
                    ? "no COM ports"
                    : string.Join(", ", devices.Select(d => d.Label));
                Status = "No Arduino Leonardo found. Plug Leonardo USB into this PC "
                         + "(Host Shield stacked, mouse into the Host Shield). Seen: "
                         + listed;
                return false;
            }

            var errors = new List<string>();
            foreach (var p in tryList)
            {
                if (TryOpenPortLocked(p, out var err))
                    return true;
                if (!string.IsNullOrWhiteSpace(err))
                    errors.Add($"{p}: {err}");
            }

            Status = "Leonardo connect failed. Flash Arduino\\leonardo_hostshield.ino as admin, then plug "
                     + "Leonardo into PC and the mouse into Host Shield. "
                     + string.Join(" | ", errors.Take(4));
            return false;
        }

        private static bool TryOpenPortLocked(string port, out string error)
        {
            error = "";
            SerialPort? sp = null;
            try
            {
                sp = new SerialPort(port, Baud)
                {
                    ReadTimeout = 400,
                    WriteTimeout = 200,
                    Encoding = Encoding.ASCII,
                    NewLine = "\n",
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = false,
                    ReadBufferSize = 1024,
                    WriteBufferSize = 256,
                };
                sp.Open();
                try { sp.DiscardInBuffer(); sp.DiscardOutBuffer(); } catch { }

                Thread.Sleep(2200);

                if (!TryHandshake(sp, out var hs, out var mouse))
                {
                    error = "no sketch handshake (flash Arduino leonardo_hostshield.ino)";
                    try { sp.Close(); } catch { }
                    try { sp.Dispose(); } catch { }
                    return false;
                }

                _hostShield = hs;
                _sawMouse = mouse;
                _port = sp;
                _activePort = port;
                Status = OnlineStatus();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try { sp?.Dispose(); } catch { }
                return false;
            }
        }

        private static bool TryHandshake(SerialPort sp, out bool hostShield, out bool sawMouse)
        {
            hostShield = false;
            sawMouse = false;
            try
            {
                var buf = new StringBuilder();
                var until = Environment.TickCount64 + 4000;
                var nextPing = Environment.TickCount64;
                int pings = 0;
                bool sawLeo = false;
                while (Environment.TickCount64 < until)
                {
                    if (pings < 12 && Environment.TickCount64 >= nextPing)
                    {
                        try
                        {
                            var ping = Encoding.ASCII.GetBytes("PING\n");
                            sp.BaseStream.Write(ping, 0, ping.Length);
                            sp.BaseStream.Flush();
                        }
                        catch { }
                        pings++;
                        nextPing = Environment.TickCount64 + 280;
                    }
                    try
                    {
                        if (sp.BytesToRead > 0)
                            buf.Append(sp.ReadExisting());
                    }
                    catch (TimeoutException) { /* keep waiting */ }

                    var text = buf.ToString();
                    if (text.IndexOf("LEONARDO", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sawLeo = true;
                        hostShield = LastFlag(text, "HS=") == "1"
                                     || text.IndexOf("HOST=1", StringComparison.OrdinalIgnoreCase) >= 0;
                        sawMouse = LastFlag(text, "MOUSE=") == "1";
                        if (hostShield)
                            return true;
                    }
                    Thread.Sleep(20);
                }
                return sawLeo;
            }
            catch { }
            return false;
        }

        private static string LastFlag(string text, string key)
        {
            var i = text.LastIndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i + key.Length >= text.Length) return "";
            return text[i + key.Length].ToString();
        }

        private static void KickReconnectLocked()
        {
            if (!IsAdministrator())
            {
                Status = "Arduino mouse method requires Administrator.";
                return;
            }

            if (Interlocked.CompareExchange(ref _reconnectBusy, 1, 0) != 0)
                return;

            var preferred = AimSettings.ArduinoComPort;
            Status = "Leonardo lost — reconnecting…";
            Task.Run(() =>
            {
                try
                {
                    lock (Gate)
                        OpenLocked(preferred);
                }
                finally
                {
                    Interlocked.Exchange(ref _reconnectBusy, 0);
                }
            });
        }

        private static bool PortOpen() => _port is { IsOpen: true };

        private static bool PortMatchesPreference(string active, string preferred)
        {
            if (string.IsNullOrWhiteSpace(preferred) || preferred.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                return true;
            return active.Equals(preferred, StringComparison.OrdinalIgnoreCase);
        }

        private static string OnlineStatus()
        {
            string hs;
            if (!_hostShield)
                hs = "Host Shield SPI fail — seat ICSP pins, 5V jumper";
            else if (!_sawMouse)
                hs = "Host Shield OK · plug mouse into shield USB";
            else
                hs = "Host Shield + mouse OK";
            return $"Leonardo {_activePort}@{Baud} ONLINE (Administrator) · {hs}";
        }

        private static void ClosePortLocked()
        {
            if (_port != null)
            {
                try { if (_port.IsOpen) _port.Close(); } catch { }
                try { _port.Dispose(); } catch { }
                _port = null;
            }
            _activePort = "";
            _hostShield = false;
            _sawMouse = false;
        }
    }
}
