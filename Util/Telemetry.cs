using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public class Telemetry
    {
        public static readonly ActivitySource MyActivitySource = new ActivitySource("MyApp.Tracing");
    }
}
