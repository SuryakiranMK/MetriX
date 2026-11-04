using System;
using System.IO;
using System.Reflection;
using System.Text;
using MetriX.Model;

namespace MetriX.Services
{
    public class DataUsageTracker : IDisposable
    {
        private readonly string _filePath;
        private readonly object _lockObj = new object();
        private DataUsageRecord _record;
        private bool _isDirty = false;
        private System.Threading.Timer _flushTimer;

        public DataUsageRecord CurrentRecord { get { return _record; } }

        public DataUsageTracker()
        {
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(appDir)) appDir = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(appDir, "data_usage.json");

            _record = Load();
            CheckDateRollovers();

            // Periodic flush every 20 seconds
            _flushTimer = new System.Threading.Timer(_ => Flush(), null, 20000, 20000);
        }

        public void RecordTraffic(ulong deltaDl, ulong deltaUl)
        {
            if (deltaDl == 0 && deltaUl == 0) return;

            lock (_lockObj)
            {
                CheckDateRollovers();

                _record.TodayDownloadBytes += deltaDl;
                _record.TodayUploadBytes += deltaUl;

                _record.WeekDownloadBytes += deltaDl;
                _record.WeekUploadBytes += deltaUl;

                _record.MonthDownloadBytes += deltaDl;
                _record.MonthUploadBytes += deltaUl;

                _record.LastRecordedDate = DateTime.Now;
                _isDirty = true;
            }
        }

        private void CheckDateRollovers()
        {
            DateTime now = DateTime.Now;

            // Day rollover
            if (now.Year != _record.CurrentYear || now.DayOfYear != _record.CurrentDayOfYear)
            {
                _record.TodayDownloadBytes = 0;
                _record.TodayUploadBytes = 0;
                _record.CurrentDayOfYear = now.DayOfYear;
            }

            // Week rollover
            int isoWeek = DataUsageRecord.GetIsoWeek(now);
            if (now.Year != _record.CurrentYear || isoWeek != _record.CurrentWeekOfYear)
            {
                _record.WeekDownloadBytes = 0;
                _record.WeekUploadBytes = 0;
                _record.CurrentWeekOfYear = isoWeek;
            }

            // Month rollover
            if (now.Year != _record.CurrentYear || now.Month != _record.CurrentMonth)
            {
                _record.MonthDownloadBytes = 0;
                _record.MonthUploadBytes = 0;
                _record.CurrentMonth = now.Month;
            }

            _record.CurrentYear = now.Year;
        }

        public void Flush()
        {
            lock (_lockObj)
            {
                if (!_isDirty) return;
                Save(_record);
                _isDirty = false;
            }
        }

        private DataUsageRecord Load()
        {
            var rec = new DataUsageRecord();
            if (!File.Exists(_filePath)) return rec;

            try
            {
                string[] lines = File.ReadAllLines(_filePath);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line == "{" || line == "}") continue;

                    int colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = line.Substring(0, colonIdx).Trim().Trim('"', ' ');
                    string val = line.Substring(colonIdx + 1).Trim().TrimEnd(',').Trim().Trim('"', ' ');

                    ulong ulVal;
                    int intVal;

                    switch (key)
                    {
                        case "CurrentYear":
                            if (int.TryParse(val, out intVal)) rec.CurrentYear = intVal;
                            break;
                        case "CurrentMonth":
                            if (int.TryParse(val, out intVal)) rec.CurrentMonth = intVal;
                            break;
                        case "CurrentDayOfYear":
                            if (int.TryParse(val, out intVal)) rec.CurrentDayOfYear = intVal;
                            break;
                        case "CurrentWeekOfYear":
                            if (int.TryParse(val, out intVal)) rec.CurrentWeekOfYear = intVal;
                            break;
                        case "TodayDownloadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.TodayDownloadBytes = ulVal;
                            break;
                        case "TodayUploadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.TodayUploadBytes = ulVal;
                            break;
                        case "WeekDownloadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.WeekDownloadBytes = ulVal;
                            break;
                        case "WeekUploadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.WeekUploadBytes = ulVal;
                            break;
                        case "MonthDownloadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.MonthDownloadBytes = ulVal;
                            break;
                        case "MonthUploadBytes":
                            if (ulong.TryParse(val, out ulVal)) rec.MonthUploadBytes = ulVal;
                            break;
                    }
                }
            }
            catch
            {
            }

            return rec;
        }

        private void Save(DataUsageRecord rec)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"CurrentYear\": {0},", rec.CurrentYear));
                sb.AppendLine(string.Format("  \"CurrentMonth\": {0},", rec.CurrentMonth));
                sb.AppendLine(string.Format("  \"CurrentDayOfYear\": {0},", rec.CurrentDayOfYear));
                sb.AppendLine(string.Format("  \"CurrentWeekOfYear\": {0},", rec.CurrentWeekOfYear));
                sb.AppendLine(string.Format("  \"TodayDownloadBytes\": {0},", rec.TodayDownloadBytes));
                sb.AppendLine(string.Format("  \"TodayUploadBytes\": {0},", rec.TodayUploadBytes));
                sb.AppendLine(string.Format("  \"WeekDownloadBytes\": {0},", rec.WeekDownloadBytes));
                sb.AppendLine(string.Format("  \"WeekUploadBytes\": {0},", rec.WeekUploadBytes));
                sb.AppendLine(string.Format("  \"MonthDownloadBytes\": {0},", rec.MonthDownloadBytes));
                sb.AppendLine(string.Format("  \"MonthUploadBytes\": {0}", rec.MonthUploadBytes));
                sb.AppendLine("}");

                File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        public static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1073741824UL)
                return string.Format("{0:F2} GB", bytes / 1073741824.0);
            if (bytes >= 1048576UL)
                return string.Format("{0:F1} MB", bytes / 1048576.0);
            if (bytes >= 1024UL)
                return string.Format("{0:F0} KB", bytes / 1024.0);
            return string.Format("{0} B", bytes);
        }

        public void Dispose()
        {
            if (_flushTimer != null)
            {
                _flushTimer.Dispose();
                _flushTimer = null;
            }
            Flush();
        }
    }
}
