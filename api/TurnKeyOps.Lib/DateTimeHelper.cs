using System;
using System.Collections.Generic;
using System.Globalization;
using TimeZoneConverter;

namespace MedInsights.Lib.Utils
{
    /// <summary>
    /// Type-safe app time zones (extend as needed).
    /// Backed by IANA IDs for portability.
    /// </summary>
    public enum AppTimeZone
    {
        Utc,
        America_New_York,
        America_Chicago,
        America_Denver,
        America_Los_Angeles,
        Europe_London,
        Europe_Paris,
        Asia_Tokyo,
        Asia_Kolkata,
        Australia_Sydney
    }

    public static class DateTimeHelper
    {
        // Canonical UTC timestamp: e.g., 20251014T192200Z
        private const string CanonicalUtcFormat = "yyyyMMdd'T'HHmmss'Z'";

        private const string CanonicalUtcFormatWithFractional = "yyyyMMdd'T'HHmmssfffffff'Z'";

        // Be lenient on parse (with/without fractional seconds).
        private static readonly string[] AcceptedUtcFormats =
        {
            "yyyyMMdd'T'HHmmss'Z'",
            "yyyyMMdd'T'HHmmssfffffff'Z'"
        };

        // IANA IDs for our enum
        private static readonly Dictionary<AppTimeZone, string> IanaIds = new()
        {
            { AppTimeZone.Utc,                "Etc/UTC" },
            { AppTimeZone.America_New_York,   "America/New_York" },
            { AppTimeZone.America_Chicago,    "America/Chicago" },
            { AppTimeZone.America_Denver,     "America/Denver" },
            { AppTimeZone.America_Los_Angeles,"America/Los_Angeles" },
            { AppTimeZone.Europe_London,      "Europe/London" },
            { AppTimeZone.Europe_Paris,       "Europe/Paris" },
            { AppTimeZone.Asia_Tokyo,         "Asia/Tokyo" },
            { AppTimeZone.Asia_Kolkata,       "Asia/Kolkata" },
            { AppTimeZone.Australia_Sydney,   "Australia/Sydney" }
        };

        private static TimeZoneInfo Resolve(AppTimeZone zone)
            => TZConvert.GetTimeZoneInfo(IanaIds[zone]);

        /// <summary>
        /// Try to map an incoming zone id (IANA or Windows) to AppTimeZone.
        /// Accepts "UTC" and "Etc/UTC" as aliases.
        /// </summary>
        public static bool TryParseAppTimeZone(string? zoneId, out AppTimeZone zone)
        {
            zone = AppTimeZone.Utc;
            if (string.IsNullOrWhiteSpace(zoneId)) return false;

            // Normalize to IANA
            string iana;
            if (TZConvert.TryWindowsToIana(zoneId, out var ianaFromWin))
                iana = ianaFromWin;
            else if (TZConvert.TryIanaToWindows(zoneId, out _))
                iana = zoneId; // already IANA
            else if (zoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                iana = "Etc/UTC";
            else
                return false;

            // Compare against your enum’s canonical IANA values
            foreach (var kv in IanaIds)
            {
                if (string.Equals(kv.Value, iana, StringComparison.OrdinalIgnoreCase))
                {
                    zone = kv.Key;
                    return true;
                }
            }
            return false;
        }        

        /// <summary>
        /// Parse to AppTimeZone or return default (UTC if not specified).
        /// </summary>
        public static AppTimeZone ParseAppTimeZoneOrDefault(string? zoneId, AppTimeZone fallback = AppTimeZone.Utc)
            => TryParseAppTimeZone(zoneId, out var z) ? z : fallback;

        private static DateTime EnsureUtc(DateTime dt)
        {
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc) // assume caller sends UTC for Unspecified
            };
        }

        // -------- Existing helpers (kept) --------

        /// <summary>
        /// Generates a UTC timestamp string in the format "yyyyMMddTHHmmssZ".
        /// Example: 20251014T192200Z
        /// </summary>
        public static string GetUniversalTimeStamp()
        {
            return GetUniversalTimeStamp(null);
        }

        /// <summary>
        /// Generates a UTC timestamp string for the provided instant (normalized to UTC).
        /// </summary>
        public static string GetUniversalTimeStamp(DateTime? timestamp = null)
        {
            var dt = timestamp.HasValue ? EnsureUtc(timestamp.Value) : DateTime.UtcNow;
            return dt.ToString(CanonicalUtcFormatWithFractional, CultureInfo.InvariantCulture); 
            //todo: transcripts were loading too fast Convert to CanonicalUtcFormat
        }

        /// <summary>
        /// Parses a UTC timestamp string (supports with/without fractional seconds) into a UTC DateTime.
        /// </summary>
        public static DateTime FromUniversalTimeStamp(string utcString)
        {
            if (string.IsNullOrWhiteSpace(utcString))
                throw new ArgumentNullException(nameof(utcString));

            var dto = DateTimeOffset.ParseExact(
                utcString,
                AcceptedUtcFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            return dto.UtcDateTime;
        }

        /// <summary>
        /// Converts a UTC timestamp string to a local DateTime in the specified time zone.
        /// </summary>
        public static DateTime FromUniversalTimeStampToLocal(string utcString, AppTimeZone targetZone)
        {
            var utc = FromUniversalTimeStamp(utcString);
            var tz = Resolve(targetZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        }

        /// <summary>
        /// Converts a UTC DateTime to a local DateTime in the specified time zone.
        /// </summary>
        public static DateTime ToLocal(DateTime utcDateTime, AppTimeZone targetZone)
        {
            var utc = EnsureUtc(utcDateTime);
            var tz = Resolve(targetZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        }

        /// <summary>
        /// Converts a UTC DateTime to a local formatted string (display only) in the specified time zone.
        /// </summary>
        public static string ToLocalString(DateTime utcDateTime, AppTimeZone targetZone, string format = "yyyy-MM-dd HH:mm:ss")
        {
            var local = ToLocal(utcDateTime, targetZone);
            return local.ToString(format, CultureInfo.InvariantCulture);
        }

        // -------- NEW: Date-only + floor/ceiling + span-to-UTC-range --------

        /// <summary>
        /// Returns the local calendar date (DateOnly) for the given UTC instant in the target time zone.
        /// </summary>
        public static DateOnly ToLocalDate(DateTime utcInstant, AppTimeZone targetZone)
        {
            var local = ToLocal(EnsureUtc(utcInstant), targetZone);
            return DateOnly.FromDateTime(local);
        }

        /// <summary>
        /// Floors a UTC instant to the UTC moment of 00:00 (midnight) of that day in the target local time zone.
        /// </summary>
        public static DateTime DateFloorUtc(DateTime utcInstant, AppTimeZone targetZone)
        {
            var tz = Resolve(targetZone);
            var utc = EnsureUtc(utcInstant);

            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var localMidnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        }

        /// <summary>
        /// Returns the UTC instant of the start of the next local day (exclusive upper bound).
        /// </summary>
        public static DateTime DateCeilingUtcExclusive(DateTime utcInstant, AppTimeZone targetZone)
        {
            var tz = Resolve(targetZone);
            var utc = EnsureUtc(utcInstant);

            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var nextLocalMidnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                                    .AddDays(1);
            return TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, tz);
        }

        /// <summary>
        /// Given an inclusive local-date span (e.g., 08/28/2025–08/30/2025) and a zone,
        /// returns a UTC range [startUtc, endUtcExclusive) that captures all events on those local days.
        /// </summary>
        public static (DateTime startUtc, DateTime endUtcExclusive)
            ToUtcRangeForLocalDateSpan(DateOnly startLocalDate, DateOnly endLocalDate, AppTimeZone targetZone)
        {
            if (endLocalDate < startLocalDate)
                throw new ArgumentException("endLocalDate must be >= startLocalDate");

            var tz = Resolve(targetZone);

            var startLocalMidnight = new DateTime(startLocalDate.Year, startLocalDate.Month, startLocalDate.Day,
                                                  0, 0, 0, DateTimeKind.Unspecified);
            var endNextLocalMidnight = new DateTime(endLocalDate.Year, endLocalDate.Month, endLocalDate.Day,
                                                    0, 0, 0, DateTimeKind.Unspecified).AddDays(1);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocalMidnight, tz);
            var endUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(endNextLocalMidnight, tz);
            return (startUtc, endUtcExclusive);
        }
    }
}
