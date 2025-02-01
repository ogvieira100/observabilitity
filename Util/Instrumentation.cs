using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public class Instrumentation : IDisposable
    {
        public const string ActivitySourceName = "Examples.AspNetCore";
        public const string ActivitySourceNameApi1 = "Api1";
        public const string ActivitySourceNameApi2 = "Api2";
        public const string ActivitySourceNameApi3 = "Api3";
        public const string MeterName = "Meter1";
        public const string MeterName2 = "Meter2";
        public const string MeterName3 = "Meter3";
        private readonly Meter meter;

        public Instrumentation()
        {
            string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
            this.ActivitySource = new ActivitySource(ActivitySourceName, version);
            this.meter = new Meter(MeterName, version);
            this.FreezingDaysCounter = this.meter.CreateCounter<long>("weather.days.freezing", description: "The number of days where the temperature is below freezing");
        }

        public ActivitySource ActivitySource { get; }

        public Counter<long> FreezingDaysCounter { get; }

        public void Dispose()
        {
            this.ActivitySource.Dispose();
            this.meter.Dispose();
        }
    }
}
