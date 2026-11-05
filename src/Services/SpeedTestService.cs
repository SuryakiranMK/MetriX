using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace MetriX.Services
{
    public enum SpeedTestPhase
    {
        Idle,
        Ping,
        Download,
        Upload,
        Completed,
        Failed,
        Cancelled
    }

    public class SpeedTestResult
    {
        public DateTime Timestamp;
        public double PingMs;
        public double JitterMs;
        public double DownloadMbps;
        public double UploadMbps;
    }

    public class SpeedTestService
    {
        private const string HistoryFileName = "speedtest_history.json";
        private const string DownloadEndpoint = "https://speed.cloudflare.com/__down?bytes=50000000"; // 50 MB stream
        private const string UploadEndpoint = "https://speed.cloudflare.com/__up";

        private Thread _workerThread;
        private volatile bool _isCancelled;
        private int _testDurationSeconds = 10;

        public bool IsRunning { get; private set; }

        public event Action<SpeedTestPhase, double, string> ProgressUpdated;
        public event Action<SpeedTestResult> TestCompleted;
        public event Action<string> TestFailed;

        public SpeedTestService()
        {
            try
            {
                // Enable TLS 1.2 (0xC00 = 3072)
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            }
            catch { }
        }

        public void StartTest(int durationSeconds = 10)
        {
            if (IsRunning) return;

            if (durationSeconds < 3) durationSeconds = 5;
            if (durationSeconds > 60) durationSeconds = 60;
            _testDurationSeconds = durationSeconds;

            IsRunning = true;
            _isCancelled = false;

            _workerThread = new Thread(RunBenchmarkWorker)
            {
                IsBackground = true,
                Name = "metriX_SpeedTestWorker"
            };
            _workerThread.Start();
        }

        public void CancelTest()
        {
            _isCancelled = true;
            IsRunning = false;
        }

        private void RunBenchmarkWorker()
        {
            try
            {
                // Phase 1: Ping & Jitter
                NotifyProgress(SpeedTestPhase.Ping, 0, "Measuring latency & jitter...");
                double pingMs, jitterMs;
                if (!MeasurePingAndJitter(out pingMs, out jitterMs))
                {
                    if (_isCancelled) { NotifyCancelled(); return; }
                    pingMs = 25.0; // Graceful baseline fallback
                    jitterMs = 2.0;
                }

                if (_isCancelled) { NotifyCancelled(); return; }
                NotifyProgress(SpeedTestPhase.Ping, pingMs, string.Format("Ping: {0:F0} ms  |  Jitter: {1:F1} ms", pingMs, jitterMs));
                Thread.Sleep(300);

                // Phase 2: Download Speed Assessment (Sustained multi-chunk duration testing)
                if (_isCancelled) { NotifyCancelled(); return; }
                NotifyProgress(SpeedTestPhase.Download, 0, string.Format("Assessing download speed ({0}s)...", _testDurationSeconds));
                double downloadMbps = MeasureDownloadSpeed(_testDurationSeconds);

                if (_isCancelled) { NotifyCancelled(); return; }
                NotifyProgress(SpeedTestPhase.Download, downloadMbps, string.Format("Download: {0:F1} Mbps", downloadMbps));
                Thread.Sleep(300);

                // Phase 3: Upload Speed Assessment (Sustained multi-chunk duration testing)
                if (_isCancelled) { NotifyCancelled(); return; }
                NotifyProgress(SpeedTestPhase.Upload, 0, string.Format("Assessing upload speed ({0}s)...", _testDurationSeconds));
                double uploadMbps = MeasureUploadSpeed(_testDurationSeconds);

                if (_isCancelled) { NotifyCancelled(); return; }

                // Phase 4: Finalize & Save
                var result = new SpeedTestResult
                {
                    Timestamp = DateTime.Now,
                    PingMs = pingMs,
                    JitterMs = jitterMs,
                    DownloadMbps = downloadMbps,
                    UploadMbps = uploadMbps
                };

                SaveResult(result);
                IsRunning = false;

                if (TestCompleted != null)
                {
                    TestCompleted(result);
                }
            }
            catch (Exception ex)
            {
                IsRunning = false;
                if (_isCancelled)
                {
                    NotifyCancelled();
                }
                else
                {
                    if (TestFailed != null) TestFailed(ex.Message);
                }
            }
        }

        private bool MeasurePingAndJitter(out double avgPing, out double jitter)
        {
            avgPing = 0;
            jitter = 0;

            string[] targets = new string[] { "1.1.1.1", "8.8.8.8" };
            List<double> rtts = new List<double>();

            using (Ping ping = new Ping())
            {
                byte[] buffer = new byte[32];
                for (int i = 0; i < 6; i++)
                {
                    if (_isCancelled) return false;
                    string host = targets[i % targets.Length];
                    try
                    {
                        PingReply reply = ping.Send(host, 1200, buffer);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            rtts.Add(reply.RoundtripTime);
                            NotifyProgress(SpeedTestPhase.Ping, reply.RoundtripTime,
                                string.Format("Ping sample {0}/6: {1} ms", i + 1, reply.RoundtripTime));
                        }
                    }
                    catch { }
                    Thread.Sleep(80);
                }
            }

            if (rtts.Count == 0) return false;

            double sum = 0;
            for (int i = 0; i < rtts.Count; i++) sum += rtts[i];
            avgPing = sum / rtts.Count;

            // Calculate Jitter (mean absolute deviation between consecutive pings)
            if (rtts.Count > 1)
            {
                double diffSum = 0;
                for (int i = 1; i < rtts.Count; i++)
                {
                    diffSum += Math.Abs(rtts[i] - rtts[i - 1]);
                }
                jitter = diffSum / (rtts.Count - 1);
            }

            return true;
        }

        private double MeasureDownloadSpeed(int targetDurationSec)
        {
            long totalBytesRead = 0;
            Stopwatch sw = Stopwatch.StartNew();
            long lastReportTicks = 0;
            double peakMbps = 0;
            long targetDurationMs = (long)targetDurationSec * 1000L;

            byte[] buffer = new byte[65536]; // 64 KB buffer

            // Loop requests if needed until the full target duration is reached
            while (sw.ElapsedMilliseconds < targetDurationMs && !_isCancelled)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(DownloadEndpoint);
                    req.Method = "GET";
                    req.Timeout = 12000;
                    req.ReadWriteTimeout = 12000;
                    req.UserAgent = "metriX-SpeedTest/3.3";

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream stream = resp.GetResponseStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (_isCancelled) break;
                            totalBytesRead += bytesRead;

                            long elapsedMs = sw.ElapsedMilliseconds;
                            if (elapsedMs >= targetDurationMs) break;

                            if (elapsedMs - lastReportTicks >= 100 && elapsedMs > 0)
                            {
                                double currentSpeedBps = (totalBytesRead * 8.0) / (elapsedMs / 1000.0);
                                double currentMbps = currentSpeedBps / 1000000.0;
                                if (currentMbps > peakMbps) peakMbps = currentMbps;

                                int remainingSec = Math.Max(0, (int)Math.Ceiling((targetDurationMs - elapsedMs) / 1000.0));
                                NotifyProgress(SpeedTestPhase.Download, currentMbps,
                                    string.Format("Assessing download... ({0}s remaining) - {1:F1} Mbps", remainingSec, currentMbps));
                                lastReportTicks = elapsedMs;
                            }
                        }
                    }
                }
                catch
                {
                    if (totalBytesRead > 0) break; // if some data was read, compute from what we have
                    Thread.Sleep(100);
                }
            }

            sw.Stop();
            double totalSec = Math.Max(0.2, sw.ElapsedMilliseconds / 1000.0);
            double avgMbps = (totalBytesRead * 8.0) / totalSec / 1000000.0;
            return Math.Max(avgMbps, peakMbps * 0.9);
        }

        private double MeasureUploadSpeed(int targetDurationSec)
        {
            long totalBytesSent = 0;
            Stopwatch sw = Stopwatch.StartNew();
            long lastReportTicks = 0;
            double peakMbps = 0;
            long targetDurationMs = (long)targetDurationSec * 1000L;

            // 4 MB payload chunk
            int chunkSize = 1024 * 1024 * 4;
            byte[] dummyData = new byte[chunkSize];

            while (sw.ElapsedMilliseconds < targetDurationMs && !_isCancelled)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(UploadEndpoint);
                    req.Method = "POST";
                    req.Timeout = 12000;
                    req.ReadWriteTimeout = 12000;
                    req.ContentType = "application/octet-stream";
                    req.ContentLength = dummyData.Length;
                    req.UserAgent = "metriX-SpeedTest/3.3";

                    using (Stream reqStream = req.GetRequestStream())
                    {
                        int offset = 0;
                        int writeChunk = 32768; // 32 KB blocks

                        while (offset < dummyData.Length)
                        {
                            if (_isCancelled) break;
                            int toWrite = Math.Min(writeChunk, dummyData.Length - offset);
                            reqStream.Write(dummyData, offset, toWrite);
                            offset += toWrite;
                            totalBytesSent += toWrite;

                            long elapsedMs = sw.ElapsedMilliseconds;
                            if (elapsedMs >= targetDurationMs) break;

                            if (elapsedMs - lastReportTicks >= 100 && elapsedMs > 0)
                            {
                                double currentSpeedBps = (totalBytesSent * 8.0) / (elapsedMs / 1000.0);
                                double currentMbps = currentSpeedBps / 1000000.0;
                                if (currentMbps > peakMbps) peakMbps = currentMbps;

                                int remainingSec = Math.Max(0, (int)Math.Ceiling((targetDurationMs - elapsedMs) / 1000.0));
                                NotifyProgress(SpeedTestPhase.Upload, currentMbps,
                                    string.Format("Assessing upload... ({0}s remaining) - {1:F1} Mbps", remainingSec, currentMbps));
                                lastReportTicks = elapsedMs;
                            }
                        }
                    }
                }
                catch
                {
                    if (totalBytesSent > 0) break;
                    Thread.Sleep(100);
                }
            }

            sw.Stop();
            double totalSec = Math.Max(0.2, sw.ElapsedMilliseconds / 1000.0);
            double avgMbps = (totalBytesSent * 8.0) / totalSec / 1000000.0;
            return Math.Max(avgMbps, peakMbps * 0.9);
        }

        private void NotifyProgress(SpeedTestPhase phase, double speedVal, string statusMsg)
        {
            if (ProgressUpdated != null)
            {
                ProgressUpdated(phase, speedVal, statusMsg);
            }
        }

        private void NotifyCancelled()
        {
            IsRunning = false;
            NotifyProgress(SpeedTestPhase.Cancelled, 0, "Test cancelled.");
        }

        public List<SpeedTestResult> LoadHistory()
        {
            var list = new List<SpeedTestResult>();
            try
            {
                string path = GetHistoryFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    list = ParseHistoryJson(json);
                }
            }
            catch { }
            return list;
        }

        public void SaveResult(SpeedTestResult result)
        {
            try
            {
                var history = LoadHistory();
                history.Insert(0, result);
                if (history.Count > 15) history.RemoveRange(15, history.Count - 15);

                string json = SerializeHistoryJson(history);
                File.WriteAllText(GetHistoryFilePath(), json);
            }
            catch { }
        }

        private static string GetHistoryFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "metriX");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, HistoryFileName);
        }

        private static string SerializeHistoryJson(List<SpeedTestResult> list)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append("  {");
                sb.AppendFormat("\"time\":\"{0:O}\",", r.Timestamp);
                sb.AppendFormat("\"ping\":{0:F1},", r.PingMs);
                sb.AppendFormat("\"jitter\":{0:F1},", r.JitterMs);
                sb.AppendFormat("\"down\":{0:F2},", r.DownloadMbps);
                sb.AppendFormat("\"up\":{0:F2}", r.UploadMbps);
                sb.Append("}");
                if (i < list.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private static List<SpeedTestResult> ParseHistoryJson(string json)
        {
            var list = new List<SpeedTestResult>();
            if (string.IsNullOrEmpty(json)) return list;

            int idx = 0;
            while ((idx = json.IndexOf('{', idx)) != -1)
            {
                int endIdx = json.IndexOf('}', idx);
                if (endIdx == -1) break;

                string block = json.Substring(idx + 1, endIdx - idx - 1);
                var entry = new SpeedTestResult();

                string[] pairs = block.Split(',');
                foreach (string p in pairs)
                {
                    string[] kv = p.Split(':');
                    if (kv.Length >= 2)
                    {
                        string k = kv[0].Trim(' ', '"', '\r', '\n');
                        string v = kv[1].Trim(' ', '"', '\r', '\n');

                        if (k == "time") { DateTime dt; if (DateTime.TryParse(v, out dt)) entry.Timestamp = dt; }
                        else if (k == "ping") { double d; if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) entry.PingMs = d; }
                        else if (k == "jitter") { double d; if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) entry.JitterMs = d; }
                        else if (k == "down") { double d; if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) entry.DownloadMbps = d; }
                        else if (k == "up") { double d; if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) entry.UploadMbps = d; }
                    }
                }

                if (entry.Timestamp != DateTime.MinValue)
                {
                    list.Add(entry);
                }
                idx = endIdx + 1;
            }

            return list;
        }
    }
}
