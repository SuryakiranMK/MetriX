using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MetriX.Services
{
    public class AppNetworkUsage
    {
        public int ProcessId;
        public string ProcessName;
        public int ActiveConnections;
        public int ProcessCount;
        public double TransferDeltaBytes;
        public double AttributionWeight;
    }

    public class ProcessNetworkTracker
    {
        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int MIB_TCP_STATE_ESTAB = 5;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            uint ulAf,
            int TableClass,
            uint Reserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

        private readonly Dictionary<int, string> _nameCache = new Dictionary<int, string>();
        private readonly Dictionary<int, ulong> _lastIoBytes = new Dictionary<int, ulong>();
        private DateTime _lastCachePrune = DateTime.Now;

        public List<AppNetworkUsage> GetTopActiveApps(int limit = 5)
        {
            var results = new List<AppNetworkUsage>();
            int size = 0;

            uint ret = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (size <= 0) return results;

            IntPtr pTable = Marshal.AllocHGlobal(size);
            try
            {
                ret = GetExtendedTcpTable(pTable, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (ret == 0 && pTable != IntPtr.Zero)
                {
                    int numEntries = Marshal.ReadInt32(pTable);
                    IntPtr rowPtr = new IntPtr(pTable.ToInt64() + 4);
                    int rowSize = 24;

                    // Aggregate active established connections per PID
                    var pidCounts = new Dictionary<int, int>();
                    var activePids = new HashSet<int>();

                    for (int i = 0; i < numEntries; i++)
                    {
                        IntPtr cur = new IntPtr(rowPtr.ToInt64() + (i * rowSize));
                        int state = Marshal.ReadInt32(cur, 0);
                        int pid = Marshal.ReadInt32(cur, 20);

                        // Only count active established connections and skip Idle/System PID
                        if (state == MIB_TCP_STATE_ESTAB && pid > 4)
                        {
                            int count;
                            pidCounts.TryGetValue(pid, out count);
                            pidCounts[pid] = count + 1;
                            activePids.Add(pid);
                        }
                    }

                    // Query process names and I/O transfer deltas
                    var pidDeltas = new Dictionary<int, double>();
                    var sbName = new StringBuilder(1024);

                    foreach (int pid in activePids)
                    {
                        string procName;
                        bool nameCached = _nameCache.TryGetValue(pid, out procName);

                        IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                        if (hProc != IntPtr.Zero)
                        {
                            try
                            {
                                if (!nameCached)
                                {
                                    sbName.Length = 0;
                                    int cap = sbName.Capacity;
                                    if (QueryFullProcessImageName(hProc, 0, sbName, ref cap) && sbName.Length > 0)
                                    {
                                        procName = Path.GetFileNameWithoutExtension(sbName.ToString());
                                    }
                                    else
                                    {
                                        try
                                        {
                                            using (var proc = Process.GetProcessById(pid))
                                            {
                                                procName = proc.ProcessName;
                                            }
                                        }
                                        catch
                                        {
                                            procName = "Process " + pid;
                                        }
                                    }
                                    _nameCache[pid] = procName;
                                }

                                IO_COUNTERS counters;
                                if (GetProcessIoCounters(hProc, out counters))
                                {
                                    ulong currentIo = counters.ReadTransferCount + counters.WriteTransferCount;
                                    ulong lastIo;
                                    if (_lastIoBytes.TryGetValue(pid, out lastIo) && currentIo >= lastIo)
                                    {
                                        pidDeltas[pid] = (double)(currentIo - lastIo);
                                    }
                                    else
                                    {
                                        pidDeltas[pid] = 0;
                                    }
                                    _lastIoBytes[pid] = currentIo;
                                }
                            }
                            catch
                            {
                            }
                            finally
                            {
                                CloseHandle(hProc);
                            }
                        }
                        else
                        {
                            if (!nameCached)
                            {
                                try
                                {
                                    using (var proc = Process.GetProcessById(pid))
                                    {
                                        procName = proc.ProcessName;
                                    }
                                }
                                catch
                                {
                                    procName = "Process " + pid;
                                }
                                _nameCache[pid] = procName;
                            }
                        }
                    }

                    // Group by Process Name (consolidation of multi-instance processes like chrome, agy, steam)
                    var grouped = new Dictionary<string, AppNetworkUsage>(StringComparer.OrdinalIgnoreCase);

                    foreach (var kvp in pidCounts)
                    {
                        int pid = kvp.Key;
                        int conns = kvp.Value;
                        string name;
                        if (!_nameCache.TryGetValue(pid, out name) || string.IsNullOrEmpty(name))
                        {
                            name = "Process " + pid;
                        }

                        double delta = 0;
                        pidDeltas.TryGetValue(pid, out delta);

                        AppNetworkUsage usage;
                        if (!grouped.TryGetValue(name, out usage))
                        {
                            usage = new AppNetworkUsage
                            {
                                ProcessId = pid,
                                ProcessName = name,
                                ActiveConnections = 0,
                                ProcessCount = 0,
                                TransferDeltaBytes = 0
                            };
                            grouped[name] = usage;
                        }

                        usage.ActiveConnections += conns;
                        usage.ProcessCount++;
                        usage.TransferDeltaBytes += delta;
                    }

                    // Compute dynamic attribution weights
                    foreach (var app in grouped.Values)
                    {
                        // Active I/O transfer gets dominant weight. When dormant, connections provide baseline.
                        // Add slight deterministic variation based on name hash so two identical connection counts
                        // without I/O delta don't produce exact identical artificial percentages.
                        double jitter = ((app.ProcessName.GetHashCode() & 0x07) * 0.04);
                        if (app.TransferDeltaBytes > 0)
                        {
                            app.AttributionWeight = app.TransferDeltaBytes + (app.ActiveConnections * 512.0) + jitter;
                        }
                        else
                        {
                            app.AttributionWeight = (app.ActiveConnections * 1.0) + jitter;
                        }
                        results.Add(app);
                    }

                    // Sort primarily by active transfer activity, secondarily by active connections
                    results.Sort((a, b) =>
                    {
                        int cmp = b.TransferDeltaBytes.CompareTo(a.TransferDeltaBytes);
                        if (cmp != 0) return cmp;
                        cmp = b.ActiveConnections.CompareTo(a.ActiveConnections);
                        if (cmp != 0) return cmp;
                        return b.AttributionWeight.CompareTo(a.AttributionWeight);
                    });

                    // Respect configured limit (0 or negative means Auto/all active consumers)
                    int maxCount = limit <= 0 ? 30 : limit;
                    if (results.Count > maxCount)
                    {
                        results.RemoveRange(maxCount, results.Count - maxCount);
                    }

                    // Periodically prune cache of dead PIDs (every 5 minutes)
                    if ((DateTime.Now - _lastCachePrune).TotalMinutes > 5)
                    {
                        var deadPids = new List<int>();
                        foreach (var pid in _lastIoBytes.Keys)
                        {
                            if (!activePids.Contains(pid)) deadPids.Add(pid);
                        }
                        foreach (var pid in deadPids)
                        {
                            _lastIoBytes.Remove(pid);
                            _nameCache.Remove(pid);
                        }
                        _lastCachePrune = DateTime.Now;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (pTable != IntPtr.Zero) Marshal.FreeHGlobal(pTable);
            }

            return results;
        }
    }
}
