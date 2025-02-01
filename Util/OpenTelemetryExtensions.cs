using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public static class OpenTelemetryExtensions
    {
        public static string ServiceName { get; }
        public static string ServiceVersion { get; }

        static OpenTelemetryExtensions()
        {
             ServiceName = "";
            ServiceVersion = "";
        }
         public static ActivitySource CreateActivitySource() =>
         new ActivitySource(ServiceName, ServiceVersion);
    }
}
