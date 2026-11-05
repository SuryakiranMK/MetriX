using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MetriX.Model;
using MetriX.Native;

namespace MetriX.Services
{
    public class NetworkHistoryPoint
    {
        public DateTime Time;
        public double DownloadBytesPerSec;
        public double UploadBytesPerSec;
    }

    public class NetworkMonitor : IDisposable
    {
        private long _lastTimestamp;
        private ulong _lastInBytes;
        private ulong _lastOutBytes;
        private bool _isFirstSample = true;

        private readonly object _historyLock = new object();
        private const int MaxHistory = 300;
        private readonly NetworkHistoryPoint[] _history = new NetworkHistoryPoint[MaxHistory];
        private int _historyCount = 0;
        private int _historyHead = 0;

        public NetworkStats CurrentStats { get; private set; }
        public ulong SessionDownloadedBytes { get; private set; }
        public ulong SessionUploadedBytes { get; private set; }
        public DateTime SessionStartTime { get; private set; }

        public string ActiveAdapterName { get; private set; }
        public string ActiveAdapterDescription { get; private set; }
        public ulong ActiveLinkSpeed { get; private set; }
        public DataUsageTracker UsageTracker { get; private set; }

        public NetworkMonitor()
        {
            CurrentStats = new NetworkStats();
            SessionStartTime = DateTime.Now;
            ActiveAdapterName = "Auto-Detect";
            ActiveAdapterDescription = "All Network Adapters";
            UsageTracker = new DataUsageTracker();

            // Pre-instantiate history points to achieve zero allocation in sampling loop
            for (int i = 0; i < MaxHistory; i++)
            {
                _history[i] = new NetworkHistoryPoint();
            }
        }

        public void ResetBaseline()
        {
            _isFirstSample = true;
        }

        public void ResetSessionStats()
        {
            SessionDownloadedBytes = 0;
            SessionUploadedBytes = 0;
            SessionStartTime = DateTime.Now;
        }

        public NetworkHistoryPoint[] GetHistorySnapshot()
        {
            lock (_historyLock)
            {
                var result = new NetworkHistoryPoint[_historyCount];
                if (_historyCount < MaxHistory)
                {
                    for (int i = 0; i < _historyCount; i++)
                    {
                        var src = _history[i];
                        result[i] = new NetworkHistoryPoint
                        {
                            Time = src.Time,
                            DownloadBytesPerSec = src.DownloadBytesPerSec,
                            UploadBytesPerSec = src.UploadBytesPerSec
                        };
                    }
                }
                else
                {
                    int tail = _historyHead; // Oldest index
                    for (int i = 0; i < MaxHistory; i++)
                    {
                        int idx = (tail + i) % MaxHistory;
                        var src = _history[idx];
                        result[i] = new NetworkHistoryPoint
                        {
                            Time = src.Time,
                            DownloadBytesPerSec = src.DownloadBytesPerSec,
                            UploadBytesPerSec = src.UploadBytesPerSec
                        };
                    }
                }
                return result;
            }
        }

        public NetworkStats Sample(AppConfig config)
        {
            ulong currentIn = 0;
            ulong currentOut = 0;

            IntPtr pTable = IntPtr.Zero;
            try
            {
                int ret = NativeMethods.GetIfTable2(out pTable);
                if (ret == 0 && pTable != IntPtr.Zero)
                {
                    uint numEntries = (uint)Marshal.ReadInt32(pTable);
                    const int rowSize = 1352;
                    int offset = IntPtr.Size == 8 ? 8 : 4;

                    string targetAlias = config != null ? config.SelectedAdapterAlias : null;
                    bool matchSpecific = !string.IsNullOrEmpty(targetAlias);

                    for (int i = 0; i < numEntries; i++)
                    {
                        IntPtr rowPtr = new IntPtr(pTable.ToInt64() + offset + (i * (long)rowSize));

                        // Offset 1152: InterfaceAndOperStatusFlags (byte)
                        byte flags = Marshal.ReadByte(rowPtr, 1152);
                        bool isFilter = (flags & 0x02) != 0;
                        if (isFilter) continue;

                        // Offset 1128: Type (int / uint). 24 is loopback
                        int type = Marshal.ReadInt32(rowPtr, 1128);
                        if (type == 24) continue;

                        if (matchSpecific)
                        {
                            string alias = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 28));
                            string desc = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 540));
                            if (string.Equals(alias, targetAlias, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(desc, targetAlias, StringComparison.OrdinalIgnoreCase))
                            {
                                currentIn += (ulong)Marshal.ReadInt64(rowPtr, 1208);
                                currentOut += (ulong)Marshal.ReadInt64(rowPtr, 1280);
                                break;
                            }
                        }
                        else
                        {
                            // Offset 1156: OperStatus (int). 1 is IfOperStatusUp
                            int operStatus = Marshal.ReadInt32(rowPtr, 1156);
                            if (operStatus == 1)
                            {
                                currentIn += (ulong)Marshal.ReadInt64(rowPtr, 1208);
                                currentOut += (ulong)Marshal.ReadInt64(rowPtr, 1280);

                                ulong txSpeed = (ulong)Marshal.ReadInt64(rowPtr, 1192);
                                ulong rxSpeed = (ulong)Marshal.ReadInt64(rowPtr, 1200);
                                ulong speed = Math.Max(txSpeed, rxSpeed);
                                if (speed > 0) ActiveLinkSpeed = speed;

                                if (string.IsNullOrEmpty(ActiveAdapterName) || ActiveAdapterName == "Auto-Detect")
                                {
                                    string alias = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 28));
                                    string desc = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 540));
                                    if (!string.IsNullOrEmpty(alias))
                                    {
                                        ActiveAdapterName = alias;
                                        ActiveAdapterDescription = desc;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Graceful fallback on query error
            }
            finally
            {
                if (pTable != IntPtr.Zero)
                {
                    NativeMethods.FreeMibTable(pTable);
                }
            }

            long currentTimestamp = Stopwatch.GetTimestamp();
            if (_isFirstSample)
            {
                _lastTimestamp = currentTimestamp;
                _lastInBytes = currentIn;
                _lastOutBytes = currentOut;
                _isFirstSample = false;

                CurrentStats.DownloadSpeedBytesPerSec = 0;
                CurrentStats.UploadSpeedBytesPerSec = 0;
                CurrentStats.TotalInBytes = currentIn;
                CurrentStats.TotalOutBytes = currentOut;
                FormatStats(CurrentStats, config);
                return CurrentStats;
            }

            double elapsedSeconds = (double)(currentTimestamp - _lastTimestamp) / Stopwatch.Frequency;

            // Handle sleep/wake or lag anomalies (> 5 seconds elapsed)
            if (elapsedSeconds <= 0.05 || elapsedSeconds > 5.0)
            {
                _lastTimestamp = currentTimestamp;
                _lastInBytes = currentIn;
                _lastOutBytes = currentOut;
                FormatStats(CurrentStats, config);
                return CurrentStats;
            }

            ulong deltaIn = currentIn >= _lastInBytes ? (currentIn - _lastInBytes) : 0;
            ulong deltaOut = currentOut >= _lastOutBytes ? (currentOut - _lastOutBytes) : 0;

            CurrentStats.DownloadSpeedBytesPerSec = deltaIn / elapsedSeconds;
            CurrentStats.UploadSpeedBytesPerSec = deltaOut / elapsedSeconds;
            CurrentStats.TotalInBytes = currentIn;
            CurrentStats.TotalOutBytes = currentOut;

            SessionDownloadedBytes += deltaIn;
            SessionUploadedBytes += deltaOut;
            if (UsageTracker != null)
            {
                UsageTracker.RecordTraffic(deltaIn, deltaOut);
            }

            lock (_historyLock)
            {
                var pt = _history[_historyHead];
                pt.Time = DateTime.Now;
                pt.DownloadBytesPerSec = CurrentStats.DownloadSpeedBytesPerSec;
                pt.UploadBytesPerSec = CurrentStats.UploadSpeedBytesPerSec;

                _historyHead = (_historyHead + 1) % MaxHistory;
                if (_historyCount < MaxHistory) _historyCount++;
            }

            _lastTimestamp = currentTimestamp;
            _lastInBytes = currentIn;
            _lastOutBytes = currentOut;

            FormatStats(CurrentStats, config);
            return CurrentStats;
        }

        public List<AdapterInfo> GetAvailableAdapters()
        {
            var list = new List<AdapterInfo>();
            IntPtr pTable = IntPtr.Zero;
            try
            {
                int ret = NativeMethods.GetIfTable2(out pTable);
                if (ret == 0 && pTable != IntPtr.Zero)
                {
                    uint numEntries = (uint)Marshal.ReadInt32(pTable);
                    const int rowSize = 1352;
                    int offset = IntPtr.Size == 8 ? 8 : 4;

                    for (int i = 0; i < numEntries; i++)
                    {
                        IntPtr rowPtr = new IntPtr(pTable.ToInt64() + offset + (i * (long)rowSize));

                        byte flags = Marshal.ReadByte(rowPtr, 1152);
                        bool isFilter = (flags & 0x02) != 0;
                        int type = Marshal.ReadInt32(rowPtr, 1128);
                        if (isFilter || type == 24) continue;

                        int operStatus = Marshal.ReadInt32(rowPtr, 1156);
                        string alias = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 28));
                        string desc = Marshal.PtrToStringUni(new IntPtr(rowPtr.ToInt64() + 540));

                        list.Add(new AdapterInfo
                        {
                            Alias = alias,
                            Description = desc,
                            Type = (uint)type,
                            IsUp = operStatus == 1
                        });
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (pTable != IntPtr.Zero)
                {
                    NativeMethods.FreeMibTable(pTable);
                }
            }
            return list;
        }

        private void FormatStats(NetworkStats stats, AppConfig config)
        {
            UnitMode mode = config != null ? config.UnitMode : UnitMode.AutoBytes;
            int decimals = config != null ? config.DecimalPlaces : 1;
            string decFormat = "F" + decimals;

            if (mode == UnitMode.AutoBits)
            {
                double downBits = stats.DownloadSpeedBytesPerSec * 8;
                double upBits = stats.UploadSpeedBytesPerSec * 8;

                string dVal, dUnit, uVal, uUnit;
                FormatValueAndUnitBits(downBits, decFormat, out dVal, out dUnit);
                FormatValueAndUnitBits(upBits, decFormat, out uVal, out uUnit);

                stats.DownloadFormatted = dVal;
                stats.DownloadUnit = dUnit;
                stats.UploadFormatted = uVal;
                stats.UploadUnit = uUnit;
            }
            else if (mode == UnitMode.FixedKB)
            {
                stats.DownloadFormatted = (stats.DownloadSpeedBytesPerSec / 1024.0).ToString(decFormat);
                stats.DownloadUnit = "KB/s";
                stats.UploadFormatted = (stats.UploadSpeedBytesPerSec / 1024.0).ToString(decFormat);
                stats.UploadUnit = "KB/s";
            }
            else if (mode == UnitMode.FixedMB)
            {
                stats.DownloadFormatted = (stats.DownloadSpeedBytesPerSec / (1024.0 * 1024.0)).ToString(decFormat);
                stats.DownloadUnit = "MB/s";
                stats.UploadFormatted = (stats.UploadSpeedBytesPerSec / (1024.0 * 1024.0)).ToString(decFormat);
                stats.UploadUnit = "MB/s";
            }
            else // AutoBytes
            {
                string dVal, dUnit, uVal, uUnit;
                FormatValueAndUnitBytes(stats.DownloadSpeedBytesPerSec, decFormat, out dVal, out dUnit);
                FormatValueAndUnitBytes(stats.UploadSpeedBytesPerSec, decFormat, out uVal, out uUnit);

                stats.DownloadFormatted = dVal;
                stats.DownloadUnit = dUnit;
                stats.UploadFormatted = uVal;
                stats.UploadUnit = uUnit;
            }
        }

        private static void FormatValueAndUnitBytes(double bytesPerSec, string format, out string val, out string unit)
        {
            if (bytesPerSec < 1024.0)
            {
                val = bytesPerSec.ToString("F0");
                unit = "B/s";
            }
            else if (bytesPerSec < 1024.0 * 1024.0)
            {
                val = (bytesPerSec / 1024.0).ToString(format);
                unit = "KB/s";
            }
            else if (bytesPerSec < 1024.0 * 1024.0 * 1024.0)
            {
                val = (bytesPerSec / (1024.0 * 1024.0)).ToString(format);
                unit = "MB/s";
            }
            else
            {
                val = (bytesPerSec / (1024.0 * 1024.0 * 1024.0)).ToString(format);
                unit = "GB/s";
            }
        }

        private static void FormatValueAndUnitBits(double bitsPerSec, string format, out string val, out string unit)
        {
            if (bitsPerSec < 1000.0)
            {
                val = bitsPerSec.ToString("F0");
                unit = "bps";
            }
            else if (bitsPerSec < 1000000.0)
            {
                val = (bitsPerSec / 1000.0).ToString(format);
                unit = "Kbps";
            }
            else if (bitsPerSec < 1000000000.0)
            {
                val = (bitsPerSec / 1000000.0).ToString(format);
                unit = "Mbps";
            }
            else
            {
                val = (bitsPerSec / 1000000000.0).ToString(format);
                unit = "Gbps";
            }
        }

        public void Dispose()
        {
            if (UsageTracker != null)
            {
                UsageTracker.Dispose();
                UsageTracker = null;
            }
        }
    }
}
