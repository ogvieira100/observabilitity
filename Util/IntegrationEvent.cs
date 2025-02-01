using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public abstract class IntegrationEvent : Event
    {
        public Guid ProcessoId { get; set; }
   
        public IntegrationEvent() : base()
        {

        }
    }
}
