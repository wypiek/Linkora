using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia.Controls;

namespace WLMClient.Compat
{
    /// <summary>
    /// The handful of things the original client did through Win32: how long the user has been
    /// idle, asking for the user's attention, and playing a short sound.
    /// </summary>
    public static class Platform
    {
        private static readonly object locker = new object();

        #region User idle time

        private static DateTime lastLocalInput = DateTime.UtcNow;
        private static bool nativeIdleUnavailable;

        /// <summary>
        /// Records activity seen by the application itself. Used as the idle source when the
        /// platform does not expose a system wide one.
        /// </summary>
        public static void NoteUserInput()
        {
            lastLocalInput = DateTime.UtcNow;
        }

        /// <summary>Seconds since the user last touched the keyboard or mouse.</summary>
        public static uint GetIdleSeconds()
        {
            if (!nativeIdleUnavailable)
            {
                try
                {
                    double seconds;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        seconds = MacNative.CGEventSourceSecondsSinceLastEventType(
                            MacNative.HIDSystemState, MacNative.AnyInputEventType);

                        return (uint)Math.Max(0, seconds);
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && LinuxNative.TryGetIdleSeconds(out seconds))
                    {
                        return (uint)Math.Max(0, seconds);
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return WindowsNative.GetIdleSeconds();
                    }
                }
                catch
                {
                    // Native idle detection is unavailable (headless, Wayland, sandbox); fall back
                    // to tracking input seen by this application.
                }

                nativeIdleUnavailable = true;
            }

            double elapsed = (DateTime.UtcNow - lastLocalInput).TotalSeconds;

            return (uint)Math.Max(0, elapsed);
        }

        #endregion

        #region Attention request

        /// <summary>
        /// The cross platform stand in for FlashWindowEx: bounces the dock icon on macOS and
        /// raises the urgency hint on X11. Does nothing where the desktop offers no equivalent.
        /// </summary>
        public static void RequestAttention(Window window)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    MacNative.RequestUserAttention();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WindowsNative.FlashWindow(window);
                }

                // On Linux the window manager owns this; Avalonia exposes no urgency hint, so the
                // notification popup and sound carry the alert instead.
            }
            catch
            {
                // Never let an attention request break message delivery.
            }
        }

        #endregion

        #region Sound

        /// <summary>
        /// Plays a WAV file. .NET has no cross platform audio API, so this hands the file to the
        /// platform's own player.
        /// </summary>
        public static void PlayWavFile(string path)
        {
            Process process = StartPlayer(path);

            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    try { ((Process)s).Dispose(); } catch { }
                };
            }
        }

        /// <summary>Launches the platform's audio player for a file, or null if there is none.</summary>
        private static Process StartPlayer(string path)
        {
            try
            {
                string fileName;
                string arguments;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    fileName = "afplay";
                    arguments = Quote(path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    fileName = FindLinuxPlayer();

                    if (fileName == null)
                    {
                        return null;
                    }

                    arguments = fileName == "ffplay"
                        ? "-nodisp -autoexit -loglevel quiet " + Quote(path)
                        : Quote(path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    fileName = "powershell";
                    arguments = "-NoProfile -Command \"(New-Object Media.SoundPlayer '" + path + "').PlaySync()\"";
                }
                else
                {
                    return null;
                }

                Process process = new Process();
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                return process;
            }
            catch
            {
                // A missing audio player must never interrupt the conversation.
                return null;
            }
        }

        private static string linuxPlayer;

        private static string FindLinuxPlayer()
        {
            lock (locker)
            {
                if (linuxPlayer != null)
                {
                    return linuxPlayer.Length == 0 ? null : linuxPlayer;
                }

                string[] candidates = { "paplay", "aplay", "pw-play", "ffplay" };

                foreach (string candidate in candidates)
                {
                    if (ExistsOnPath(candidate))
                    {
                        linuxPlayer = candidate;
                        return candidate;
                    }
                }

                linuxPlayer = string.Empty;

                return null;
            }
        }

        private static bool ExistsOnPath(string command)
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
            {
                return false;
            }

            foreach (string directory in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }

                try
                {
                    if (File.Exists(Path.Combine(directory, command)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Unreadable PATH entry.
                }
            }

            return false;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        #endregion

        #region Native interop

        private static class MacNative
        {
            private const string ApplicationServices =
                "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
            private const string ObjC = "/usr/lib/libobjc.dylib";
            private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

            internal const int HIDSystemState = 1;
            internal const uint AnyInputEventType = 0xFFFFFFFF;

            /// <summary>NSInformationalRequest: bounces the dock icon once.</summary>
            private const long NSInformationalRequest = 10;

            [DllImport(ApplicationServices)]
            internal static extern double CGEventSourceSecondsSinceLastEventType(int stateID, uint eventType);

            [DllImport(ObjC, EntryPoint = "objc_getClass")]
            private static extern IntPtr GetClass(string name);

            [DllImport(ObjC, EntryPoint = "sel_registerName")]
            private static extern IntPtr RegisterSelector(string name);

            [DllImport(ObjC, EntryPoint = "objc_msgSend")]
            private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector);

            [DllImport(ObjC, EntryPoint = "objc_msgSend")]
            private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector, long argument);

            internal static void RequestUserAttention()
            {
                // Touch AppKit so the framework is loaded before we message NSApplication.
                NativeLibrary.TryLoad(AppKit, out _);

                IntPtr applicationClass = GetClass("NSApplication");
                if (applicationClass == IntPtr.Zero)
                {
                    return;
                }

                IntPtr shared = SendMessage(applicationClass, RegisterSelector("sharedApplication"));
                if (shared == IntPtr.Zero)
                {
                    return;
                }

                SendMessage(shared, RegisterSelector("requestUserAttention:"), NSInformationalRequest);
            }
        }

        private static class LinuxNative
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct XScreenSaverInfo
            {
                public IntPtr Window;
                public int State;
                public int Kind;
                public ulong TilOrSince;
                public ulong Idle;
                public ulong EventMask;
            }

            [DllImport("libX11.so.6")]
            private static extern IntPtr XOpenDisplay(IntPtr display);

            [DllImport("libX11.so.6")]
            private static extern IntPtr XDefaultRootWindow(IntPtr display);

            [DllImport("libXss.so.1")]
            private static extern IntPtr XScreenSaverAllocInfo();

            [DllImport("libXss.so.1")]
            private static extern int XScreenSaverQueryInfo(IntPtr display, IntPtr drawable, IntPtr info);

            [DllImport("libX11.so.6")]
            private static extern int XFree(IntPtr data);

            private static IntPtr display = IntPtr.Zero;
            private static bool displayResolved;

            internal static bool TryGetIdleSeconds(out double seconds)
            {
                seconds = 0;

                if (!displayResolved)
                {
                    display = XOpenDisplay(IntPtr.Zero);
                    displayResolved = true;
                }

                if (display == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr info = XScreenSaverAllocInfo();
                if (info == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    if (XScreenSaverQueryInfo(display, XDefaultRootWindow(display), info) == 0)
                    {
                        return false;
                    }

                    XScreenSaverInfo value = Marshal.PtrToStructure<XScreenSaverInfo>(info);
                    seconds = value.Idle / 1000.0;

                    return true;
                }
                finally
                {
                    XFree(info);
                }
            }
        }

        private static class WindowsNative
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct LASTINPUTINFO
            {
                public uint cbSize;
                public uint dwTime;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct FLASHWINFO
            {
                public uint cbSize;
                public IntPtr hwnd;
                public uint dwFlags;
                public uint uCount;
                public uint dwTimeout;
            }

            private const uint FLASHW_ALL = 3;
            private const uint FLASHW_TIMER = 4;

            [DllImport("user32.dll")]
            private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

            [DllImport("user32.dll")]
            private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

            internal static uint GetIdleSeconds()
            {
                LASTINPUTINFO info = new LASTINPUTINFO();
                info.cbSize = (uint)Marshal.SizeOf(info);
                info.dwTime = 0;

                uint ticks = (uint)Environment.TickCount;

                if (GetLastInputInfo(ref info))
                {
                    uint idle = ticks - info.dwTime;

                    return idle > 0 ? idle / 1000 : 0;
                }

                return 0;
            }

            internal static void FlashWindow(Window window)
            {
                if (window == null || window.TryGetPlatformHandle() == null)
                {
                    return;
                }

                FLASHWINFO info = new FLASHWINFO
                {
                    hwnd = window.TryGetPlatformHandle().Handle,
                    dwFlags = FLASHW_ALL | FLASHW_TIMER,
                    uCount = 5,
                    dwTimeout = 0
                };

                info.cbSize = (uint)Marshal.SizeOf(info);

                FlashWindowEx(ref info);
            }
        }

        #endregion
    }
}
