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
    public class EnderecoMapping : IEntityTypeConfiguration<Endereco>
    {
        public void Configure(EntityTypeBuilder<Endereco> builder)
        {
            builder.HasKey(e => e.Id); // Define a chave prmária
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            builder
              .Property(e => e.Bairro)
              .HasColumnName("Bairro")
              .HasMaxLength(50)
              .IsRequired(true);

            builder
              .Property(e => e.Logradouro)
              .HasColumnName("Logradouro")
              .HasMaxLength(100)
              .IsRequired(true);


            builder
              .Property(e => e.Cep)
              .HasColumnName("Cep")
              .HasMaxLength(20)
              .IsRequired(true);


            builder.HasOne(e => e.Cliente)
              .WithMany(c => c.Enderecos)
              .HasForeignKey(e => e.ClienteId);     

            builder.ToTable("Endereco");
        }
    }
}
