using System;
using System.Globalization;

namespace MetriX.Model
{
    public class DataUsageRecord
    {
        public DateTime LastRecordedDate { get; set; }
        public int CurrentDayOfYear { get; set; }
        public int CurrentWeekOfYear { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        public ulong TodayDownloadBytes { get; set; }
        public ulong TodayUploadBytes { get; set; }

        public ulong WeekDownloadBytes { get; set; }
        public ulong WeekUploadBytes { get; set; }

        public ulong MonthDownloadBytes { get; set; }
        public ulong MonthUploadBytes { get; set; }

        public ulong TodayTotalBytes { get { return TodayDownloadBytes + TodayUploadBytes; } }
        public ulong WeekTotalBytes { get { return WeekDownloadBytes + WeekUploadBytes; } }
        public ulong MonthTotalBytes { get { return MonthDownloadBytes + MonthUploadBytes; } }

        public DataUsageRecord()
        {
            DateTime now = DateTime.Now;
            LastRecordedDate = now;
            CurrentDayOfYear = now.DayOfYear;
            CurrentMonth = now.Month;
            CurrentYear = now.Year;
            CurrentWeekOfYear = GetIsoWeek(now);
        }

        public static int GetIsoWeek(DateTime dt)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(dt, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}

