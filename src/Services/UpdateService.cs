using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MetriX.Model;

namespace MetriX.Services
{
    public class UpdateService : IDisposable
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/SuryakiranMK/MetriX/releases/latest";
        private const string UserAgent = "metriX-AutoUpdater/2.5.0";

        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly SynchronizationContext _syncContext;

        private System.Threading.Timer _connectivityTimer;
        private bool _hasCheckedThisSession = false;
        private int _networkStableCounter = 0;
        private bool _isChecking = false;
        private bool _isDownloading = false;

        public event Action<UpdateInfo> UpdateAvailable;
        public event Action<string> UpdateCheckCompleted;

        public bool IsChecking { get { return _isChecking; } }
        public bool IsDownloading { get { return _isDownloading; } }
        public bool HasCheckedThisSession { get { return _hasCheckedThisSession; } }

        public UpdateService(ConfigService configService, NetworkMonitor networkMonitor)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

            // Enable TLS 1.2 for GitHub API compatibility on .NET 4.8
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }

            StartSessionConnectivityWatcher();
        }

        public void StartSessionConnectivityWatcher()
        {
            if (_hasCheckedThisSession || !_configService.Config.EnableAutoUpdateCheck)
                return;

            // Poll connectivity every 4 seconds until internet is confirmed active
            _connectivityTimer = new System.Threading.Timer(OnConnectivityPollTick, null, 3000, 4000);
        }

        private void OnConnectivityPollTick(object state)
        {
            if (_hasCheckedThisSession)
            {
                StopConnectivityTimer();
                return;
            }

            try
            {
                // Check whether physical network interface and internet link are available
                bool isNetworkUp = NetworkInterface.GetIsNetworkAvailable();
                bool hasTraffic = _networkMonitor != null && _networkMonitor.CurrentStats != null;

                if (isNetworkUp)
                {
                    _networkStableCounter++;
                    // Wait for 4 successful consecutive polls (~16-20 seconds post-connectivity)
                    // to prevent contending with Windows startup boot sequence
                    if (_networkStableCounter >= 4)
                    {
                        StopConnectivityTimer();
                        _hasCheckedThisSession = true;

                        // Execute background session check
                        CheckForUpdatesAsync(null, true);
                    }
                }
                else
                {
                    _networkStableCounter = 0;
                }
            }
            catch
            {
                _networkStableCounter = 0;
            }
        }

        private void StopConnectivityTimer()
        {
            if (_connectivityTimer != null)
            {
                _connectivityTimer.Dispose();
                _connectivityTimer = null;
            }
        }

        public void CheckForUpdatesAsync(Action<UpdateInfo> callback = null, bool isBackgroundSession = false)
        {
            if (_isChecking) return;
            _isChecking = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                UpdateInfo result = null;
                try
                {
                    result = FetchLatestReleaseInfo();
                }
                catch (Exception ex)
                {
                    result = new UpdateInfo
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = ex.Message
                    };
                }
                finally
                {
                    _isChecking = false;
                }

                _syncContext.Post(__ =>
                {
                    if (result != null && result.IsUpdateAvailable)
                    {
                        // Check if silent update mode is active
                        if (isBackgroundSession && _configService.Config.EnableSilentAutoUpdate)
                        {
                            // Automatically download and install without interrupting user
                            StartSilentDownloadAndApply(result);
                        }
                        else
                        {
                            if (callback != null)
                            {
                                callback(result);
                            }
                            else if (UpdateAvailable != null)
                            {
                                UpdateAvailable(result);
                            }
                        }
                    }
                    else
                    {
                        if (callback != null)
                        {
                            callback(result);
                        }
                        if (UpdateCheckCompleted != null)
                        {
                            UpdateCheckCompleted(result != null && string.IsNullOrEmpty(result.ErrorMessage)
                                ? "Latest version installed"
                                : result != null ? result.ErrorMessage : "Check failed");
                        }
                    }
                }, null);
            });
        }

        private UpdateInfo FetchLatestReleaseInfo()
        {
            var info = new UpdateInfo();
            info.CurrentVersion = GetCurrentAssemblyVersion();

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                client.Headers[HttpRequestHeader.Accept] = "application/vnd.github.v3+json";
                client.Encoding = Encoding.UTF8;

                string json = client.DownloadString(GitHubApiUrl);
                if (string.IsNullOrEmpty(json))
                {
                    info.ErrorMessage = "Empty response received from GitHub.";
                    return info;
                }

                string tag = ExtractJsonValue(json, "tag_name");
                info.VersionTag = tag;
                info.NewVersion = ParseVersionSafely(tag);
                info.ReleaseTitle = ExtractJsonValue(json, "name");
                info.Changelog = ExtractJsonValue(json, "body");

                string publishedStr = ExtractJsonValue(json, "published_at");
                DateTime pubDate;
                if (DateTime.TryParse(publishedStr, out pubDate))
                {
                    info.PublishedAt = pubDate;
                }

                ParseAssets(json, info);

                // Compare versions
                if (info.NewVersion > info.CurrentVersion)
                {
                    info.IsUpdateAvailable = true;
                }
            }

            return info;
        }

        private void ParseAssets(string json, UpdateInfo info)
        {
            int assetsIdx = json.IndexOf("\"assets\":");
            if (assetsIdx < 0) return;

            int endAssets = json.IndexOf(']', assetsIdx);
            if (endAssets < 0) endAssets = json.Length;

            string assetsSub = json.Substring(assetsIdx, endAssets - assetsIdx);
            int cursor = 0;

            while (cursor < assetsSub.Length)
            {
                int nameIdx = assetsSub.IndexOf("\"name\":", cursor);
                if (nameIdx < 0) break;

                int nameStart = assetsSub.IndexOf('"', nameIdx + 7) + 1;
                int nameEnd = assetsSub.IndexOf('"', nameStart);
                if (nameStart <= 0 || nameEnd <= nameStart) break;

                string assetName = assetsSub.Substring(nameStart, nameEnd - nameStart);

                int urlIdx = assetsSub.IndexOf("\"browser_download_url\":", nameEnd);
                if (urlIdx < 0) break;

                int urlStart = assetsSub.IndexOf('"', urlIdx + 23) + 1;
                int urlEnd = assetsSub.IndexOf('"', urlStart);
                if (urlStart <= 0 || urlEnd <= urlStart) break;

                string downloadUrl = assetsSub.Substring(urlStart, urlEnd - urlStart);

                if (string.Equals(assetName, "metriX.exe", StringComparison.OrdinalIgnoreCase))
                {
                    info.ExeDownloadUrl = downloadUrl;
                }
                else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.ZipDownloadUrl))
                {
                    info.ZipDownloadUrl = downloadUrl;
                }

                cursor = urlEnd + 1;
            }
        }

        public void DownloadUpdateAsync(UpdateInfo info, Action<int, long, long> progressCallback, Action<string, Exception> completeCallback)
        {
            if (_isDownloading) return;
            _isDownloading = true;

            string downloadUrl = !string.IsNullOrEmpty(info.ExeDownloadUrl) ? info.ExeDownloadUrl : info.ZipDownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                _isDownloading = false;
                if (completeCallback != null)
                {
                    completeCallback(null, new Exception("No valid download package found in release."));
                }
                return;
            }

            bool isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            string tempDir = Path.Combine(Path.GetTempPath(), "metriX_update");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            string targetFile = isZip
                ? Path.Combine(tempDir, "update_package.zip")
                : Path.Combine(tempDir, "metriX_new.exe");

            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = UserAgent;

            client.DownloadProgressChanged += (s, e) =>
            {
                _syncContext.Post(_ =>
                {
                    if (progressCallback != null)
                    {
                        progressCallback(e.ProgressPercentage, e.BytesReceived, e.TotalBytesToReceive);
                    }
                }, null);
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                _isDownloading = false;
                client.Dispose();

                if (e.Error != null)
                {
                    _syncContext.Post(_ =>
                    {
                        if (completeCallback != null) completeCallback(null, e.Error);
                    }, null);
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    string readyExePath = targetFile;
                    Exception processEx = null;

                    if (isZip)
                    {
                        try
                        {
                            string extractDir = Path.Combine(tempDir, "extracted");
                            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                            ZipFile.ExtractToDirectory(targetFile, extractDir);

                            string[] foundExes = Directory.GetFiles(extractDir, "metriX.exe", SearchOption.AllDirectories);
                            if (foundExes.Length > 0)
                            {
                                readyExePath = foundExes[0];
                            }
                            else
                            {
                                processEx = new FileNotFoundException("Could not find metriX.exe in downloaded archive.");
                            }
                        }
                        catch (Exception ex)
                        {
                            processEx = ex;
                        }
                    }

                    _syncContext.Post(__ =>
                    {
                        if (completeCallback != null) completeCallback(readyExePath, processEx);
                    }, null);
                });
            };

            try
            {
                client.DownloadFileAsync(new Uri(downloadUrl), targetFile);
            }
            catch (Exception ex)
            {
                _isDownloading = false;
                client.Dispose();
                if (completeCallback != null) completeCallback(null, ex);
            }
        }

        private void StartSilentDownloadAndApply(UpdateInfo info)
        {
            DownloadUpdateAsync(info, null, (readyExePath, err) =>
            {
                if (err == null && !string.IsNullOrEmpty(readyExePath) && File.Exists(readyExePath))
                {
                    ApplyUpdateAndRestart(readyExePath);
                }
            });
        }

        public static void ApplyUpdateAndRestart(string newExePath)
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                string parentDir = Path.GetDirectoryName(currentExePath);
                string scriptPath = Path.Combine(Path.GetTempPath(), "metriX_swap.cmd");

                // Micro-transition script: waits for current process to terminate, replaces binary, and relaunches
                string cmd = string.Format(
                    "@echo off\r\n" +
                    "chcp 65001 >nul\r\n" +
                    ":: Wait briefly for current process to release\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
                    ":: Ensure PID is terminated\r\n" +
                    "taskkill /f /pid {0} >nul 2>&1\r\n" +
                    ":retry\r\n" +
                    "copy /y \"{1}\" \"{2}\" >nul 2>&1\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "    timeout /t 1 /nobreak >nul\r\n" +
                    "    goto retry\r\n" +
                    ")\r\n" +
                    "cd /d \"{3}\"\r\n" +
                    "start \"\" \"{2}\"\r\n" +
                    "del /f /q \"{1}\" >nul 2>&1\r\n" +
                    "del /f /q \"%~f0\" >nul 2>&1\r\n",
                    currentPid, newExePath, currentExePath, parentDir);

                File.WriteAllText(scriptPath, cmd, Encoding.UTF8);

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c \"{0}\"", scriptPath),
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };

                Process.Start(psi);
                Environment.Exit(0);
            }
            catch
            {
                Environment.Exit(0);
            }
        }

        public static Version GetCurrentAssemblyVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch
            {
                return new Version(2, 5, 0, 0);
            }
        }

        public static Version ParseVersionSafely(string verStr)
        {
            if (string.IsNullOrEmpty(verStr)) return new Version(0, 0, 0, 0);
            try
            {
                string clean = verStr.Trim().TrimStart('v', 'V');
                string[] parts = clean.Split('.');
                int major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
                int minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                int build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                int rev = parts.Length > 3 ? int.Parse(parts[3]) : 0;
                return new Version(major, minor, build, rev);
            }
            catch
            {
                return new Version(0, 0, 0, 0);
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search);
            if (idx < 0) return "";

            int valStart = idx + search.Length;
            while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;

            if (valStart >= json.Length) return "";

            if (json[valStart] == '"')
            {
                valStart++;
                var sb = new StringBuilder();
                bool escaped = false;
                for (int i = valStart; i < json.Length; i++)
                {
                    char c = json[i];
                    if (escaped)
                    {
                        if (c == 'n') sb.Append('\n');
                        else if (c == 'r') sb.Append('\r');
                        else if (c == 't') sb.Append('\t');
                        else if (c == '"') sb.Append('"');
                        else if (c == '\\') sb.Append('\\');
                        else sb.Append(c);
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
            else
            {
                int end = valStart;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != '\r' && json[end] != '\n')
                {
                    end++;
                }
                return json.Substring(valStart, end - valStart).Trim();
            }
        }

        public void Dispose()
        {
            StopConnectivityTimer();
        }
    }
}

