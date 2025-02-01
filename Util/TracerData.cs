using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public class TracerData
    {
        public string? TraceId { get; set; }
        public string? TraceState { get; set; }
        public string? SpanId { get; set; }
    }
}
