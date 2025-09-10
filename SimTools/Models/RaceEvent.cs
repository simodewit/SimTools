using System;

namespace SimTools.Models
{
    public class RaceEvent
    {
        public DateTime DateUtc { get; set; }          // canonical timestamp (UTC)
        public string Series { get; set; } = "";
        public string Event { get; set; } = "";
        public string Circuit { get; set; } = "";
        public string Country { get; set; } = "";
        public string Link { get; set; } = "";

        // Previously read-only; now writable so WPF won't complain if a TwoWay binding occurs.
        public DateTime DateLocal
        {
            get => DateUtc.Kind == DateTimeKind.Utc ? DateUtc.ToLocalTime() : DateUtc;
            set
            {
                // Accept values as local time by default; store canonically in UTC
                if(value.Kind == DateTimeKind.Utc)
                    DateUtc = value;
                else
                    DateUtc = DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
            }
        }
    }
}
