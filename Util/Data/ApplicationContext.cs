using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util.Mapping;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Util.Data
{
    public class ApplicationContext:DbContext
    {


        public ApplicationContext(DbContextOptions<ApplicationContext> options)
        : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new EnderecoMapping());
            modelBuilder.ApplyConfiguration(new ClienteMapping());
            modelBuilder.ApplyConfiguration(new MovimentacaoBancariaMapping());

            base.OnModelCreating(modelBuilder); 
        }

    }
}
