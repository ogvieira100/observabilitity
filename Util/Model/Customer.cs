using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util.Model
{

    public class CustomerRequest
    {
        public long Id { get; set; }

        public string Nome { get; set; }
        public string CPF { get; set; }
    }

    public enum TipoEndereco
    {

        Comercial = 1,
        Residencial = 2,
        Cobranca = 3    
    }
    public class Endereco {
        public long Id { get; set; }

        public TipoEndereco TipoEndereco { get; set; }
        public string Bairro { get; set; }
        public string Logradouro { get; set; }
        public string Cep { get; set; }

        
        public virtual Cliente Cliente { get; set; }
        public long ClienteId { get; set; }

        public Endereco()
        {
            TipoEndereco = TipoEndereco.Residencial;
        }

    }

    public enum TipoMovimentacao { 
    
        Credito = 1,
        Debito = 2  

    }
    public class MovimentacaoBancaria {

        public long Id { get; set; }
        public decimal Valor { get; set; }
        public TipoMovimentacao TipoMovimentacao { get; set; }
        public DateTime DataMovimentacao { get; set; }

        
        public virtual Cliente Cliente { get; set; }
        public long ClienteId { get; set; }
        public MovimentacaoBancaria()
        {
           
        }

    }


    public class Cliente 
    {
        public long Id { get; set; }
        public string Nome { get; set; }
        public string CPF { get; set; }

        
        public virtual List<Endereco> Enderecos { get; set; }

        
        public virtual List<MovimentacaoBancaria> MovimentacaoBancarias { get; set; }

        public Cliente()
        {
            Enderecos = new List<Endereco>();
            MovimentacaoBancarias = new List<MovimentacaoBancaria>();   
        }

    }
}

