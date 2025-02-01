using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public  class InserirMovimentacaoBancariaIntegrationEvent: IntegrationEvent
    {
        public long ClientId { get; set; }
    }
}
