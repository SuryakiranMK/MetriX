using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MetriX.Services
{
    public class AppNetworkUsage
    {
        public int ProcessId;
        public string ProcessName;
        public int ActiveConnections;
    }

    public class ProcessNetworkTracker
    {
        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int MIB_TCP_STATE_ESTAB = 5;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            uint ulAf,
            int TableClass,
            uint Reserved);

        private readonly Dictionary<int, string> _nameCache = new Dictionary<int, string>();
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

                    Dictionary<int, int> pidCounts = new Dictionary<int, int>();

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
                        }
                    }

                    // Periodically prune cache
                    if ((DateTime.Now - _lastCachePrune).TotalMinutes > 5)
                    {
                        _nameCache.Clear();
                        _lastCachePrune = DateTime.Now;
                    }

                    foreach (var kvp in pidCounts)
                    {
                        string procName;
                        if (!_nameCache.TryGetValue(kvp.Key, out procName))
                        {
                            try
                            {
                                using (var proc = Process.GetProcessById(kvp.Key))
                                {
                                    procName = proc.ProcessName;
                                }
                            }
                            catch
                            {
                                procName = "Process " + kvp.Key;
                            }
                            _nameCache[kvp.Key] = procName;
                        }

                        results.Add(new AppNetworkUsage
                        {
                            ProcessId = kvp.Key,
                            ProcessName = procName,
                            ActiveConnections = kvp.Value
                        });
                    }

                    results.Sort((a, b) => b.ActiveConnections.CompareTo(a.ActiveConnections));
                    if (results.Count > limit)
                    {
                        results.RemoveRange(limit, results.Count - limit);
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
