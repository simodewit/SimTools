using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimTools.Models;

namespace SimTools.Services
{
    public class RaceCalendarService
    {
        public async Task<IList<RaceEvent>> FetchUpcomingAsync(int max = 200)
        {
            await Task.Yield();

            var nowUtc = DateTime.UtcNow;

            var seeds = new[]
            {
                new { Series = "F1",      Circuit = "Monza",                 Country = "Italy",   DayOfWeek = DayOfWeek.Sunday,  StartHourUtc = 13 },
                new { Series = "WEC",     Circuit = "Fuji Speedway",         Country = "Japan",   DayOfWeek = DayOfWeek.Saturday,StartHourUtc = 3  },
                new { Series = "IndyCar", Circuit = "Laguna Seca",           Country = "USA",     DayOfWeek = DayOfWeek.Sunday,  StartHourUtc = 20 },
                new { Series = "IMSA",    Circuit = "Road Atlanta",          Country = "USA",     DayOfWeek = DayOfWeek.Saturday,StartHourUtc = 18 },
                new { Series = "F2",      Circuit = "Yas Marina",            Country = "UAE",     DayOfWeek = DayOfWeek.Saturday,StartHourUtc = 10 },
                new { Series = "GT World",Circuit = "Spa-Francorchamps",     Country = "Belgium", DayOfWeek = DayOfWeek.Sunday,  StartHourUtc = 12 },
            };

            var list = new List<RaceEvent>(max);

            // Build 24 months of events (lots to show)
            var firstOfThisMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            for(int m = 0; m < 24; m++)
            {
                var monthStart = firstOfThisMonthUtc.AddMonths(m);

                foreach(var s in seeds)
                {
                    var date = FirstDayOfWeekInMonth(monthStart, s.DayOfWeek).AddHours(s.StartHourUtc);

                    list.Add(new RaceEvent
                    {
                        Series = s.Series,
                        Event = $"{s.Series} Round {m + 1}",
                        Circuit = s.Circuit,
                        Country = s.Country,
                        DateUtc = date,
                        Link = LinkForSeries(s.Series)
                    });
                }
            }

            return list
                .Where(e => e.DateUtc >= nowUtc.AddDays(-1)) // keep future (and a tiny recent past)
                .OrderBy(e => e.DateUtc)
                .Take(Math.Max(1, max))
                .ToList();
        }

        private static DateTime FirstDayOfWeekInMonth(DateTime monthStartUtc, DayOfWeek desired)
        {
            int offset = ((int)desired - (int)monthStartUtc.DayOfWeek + 7) % 7;
            return monthStartUtc.AddDays(offset);
        }

        private static string LinkForSeries(string series) => series switch
        {
            "F1" => "https://www.formula1.com/en/racing",
            "WEC" => "https://www.fiawec.com/",
            "IndyCar" => "https://www.indycar.com/Schedule",
            "IMSA" => "https://www.imsa.com/schedule/",
            "F2" => "https://www.fiaformula2.com/Calendar",
            "GT World" => "https://www.gt-world-challenge-europe.com/calendar",
            _ => "https://en.wikipedia.org/wiki/Motorsport_calendar"
        };
    }
}
