using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util.Model;

namespace Util.Mapping
{
    public class MovimentacaoBancariaMapping : IEntityTypeConfiguration<MovimentacaoBancaria>
    {
        public void Configure(EntityTypeBuilder<MovimentacaoBancaria> builder)
        {
            builder.HasKey(e => e.Id); // Define a chave prmária
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            builder
              .Property(e => e.Valor)
              .HasColumnName("Valor")
              .HasPrecision(18, 10)
              .IsRequired(true);

            builder
              .Property(e => e.TipoMovimentacao)
              .HasColumnName("TipoMovimentacao")
              .IsRequired(true);

            builder
            .Property(e => e.DataMovimentacao)
            .HasColumnName("DataMovimentacao")
            .IsRequired(true);

            builder.HasOne(e => e.Cliente)
              .WithMany(c => c.MovimentacaoBancarias)
              .HasForeignKey(e => e.ClienteId); 
        }
    }
}
