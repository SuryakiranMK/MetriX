using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MetriX.Model;
using MetriX.Native;

namespace MetriX.Services
{
    public class NetworkMonitor
    {
        private long _lastTimestamp;
        private ulong _lastInBytes;
        private ulong _lastOutBytes;
        private bool _isFirstSample = true;

        public NetworkStats CurrentStats { get; private set; }

        public NetworkMonitor()
        {
            CurrentStats = new NetworkStats();
        }

        public void ResetBaseline()
        {
            _isFirstSample = true;
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
                    int rowSize = Marshal.SizeOf(typeof(NativeMethods.MIB_IF_ROW2));
                    int offset = IntPtr.Size == 8 ? 8 : 4;

                    string targetAlias = config != null ? config.SelectedAdapterAlias : null;
                    bool matchSpecific = !string.IsNullOrEmpty(targetAlias);

                    for (int i = 0; i < numEntries; i++)
                    {
                        IntPtr rowPtr = new IntPtr(pTable.ToInt64() + offset + (i * (long)rowSize));
                        var row = (NativeMethods.MIB_IF_ROW2)Marshal.PtrToStructure(rowPtr, typeof(NativeMethods.MIB_IF_ROW2));

                        // Filter out NDIS light-weight filter drivers
                        bool isFilter = (row.InterfaceAndOperStatusFlags & 0x02) != 0;
                        if (isFilter)
                            continue;

                        // Filter out loopback (Type 24)
                        if (row.Type == 24)
                            continue;

                        if (matchSpecific)
                        {
                            if (string.Equals(row.Alias, targetAlias, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(row.Description, targetAlias, StringComparison.OrdinalIgnoreCase))
                            {
                                currentIn += row.InOctets;
                                currentOut += row.OutOctets;
                                break;
                            }
                        }
                        else
                        {
                            // Count all active interfaces (OperStatus == 1 is IfOperStatusUp)
                            if (row.OperStatus == 1)
                            {
                                currentIn += row.InOctets;
                                currentOut += row.OutOctets;
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
                    int rowSize = Marshal.SizeOf(typeof(NativeMethods.MIB_IF_ROW2));
                    int offset = IntPtr.Size == 8 ? 8 : 4;

                    for (int i = 0; i < numEntries; i++)
                    {
                        IntPtr rowPtr = new IntPtr(pTable.ToInt64() + offset + (i * (long)rowSize));
                        var row = (NativeMethods.MIB_IF_ROW2)Marshal.PtrToStructure(rowPtr, typeof(NativeMethods.MIB_IF_ROW2));

                        bool isFilter = (row.InterfaceAndOperStatusFlags & 0x02) != 0;
                        if (isFilter || row.Type == 24)
                            continue;

                        list.Add(new AdapterInfo
                        {
                            Alias = row.Alias,
                            Description = row.Description,
                            Type = row.Type,
                            IsUp = row.OperStatus == 1
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
    }
}
