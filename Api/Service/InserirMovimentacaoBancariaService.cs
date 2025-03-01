﻿using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Util.Data;
using Util.Model;

namespace Api.Service
{

    public interface IInserirMovimentacaoBancariaService { 

        Task InserirMovimentacaoBancariaAsync(CustomerRequest customerRequest);  

    }
    public class InserirMovimentacaoBancariaService : IInserirMovimentacaoBancariaService
    {

        readonly ApplicationContext _applicationContext;

        readonly ILogger<InserirMovimentacaoBancariaService> _logger;

        public InserirMovimentacaoBancariaService(ILogger<InserirMovimentacaoBancariaService> logger, ApplicationContext applicationContext)
        {
            _logger = logger;   
            _applicationContext = applicationContext;   
        }
        public async Task InserirMovimentacaoBancariaAsync(CustomerRequest customerRequest)
        {
            var client = await _applicationContext.Set<Cliente>().FirstOrDefaultAsync(x => x.Id == customerRequest.Id);
           _logger.LogInformation($"Cadastrando movimentação bancária para o cliente {(client.Nome)}  "); 
            decimal[] decimals = { 100.50m, -200.50m, 3010.10m, -400m, 500.15m, 60010.50m, 701.55m, -810.50m, 910.55m, -10.00m, 55.10m };
            for (int i = 1; i <= 100; i++)
            {
                var rand = new Random();
                var pos = rand.Next(0, (decimals.Length - 1));
                var valPos = decimals[pos];
                var tipoMov = valPos > 0 ? TipoMovimentacao.Credito : TipoMovimentacao.Debito;

                var mov = new MovimentacaoBancaria
                {
                    DataMovimentacao = DateTime.Now,
                    TipoMovimentacao = tipoMov,
                    Valor = valPos
                };

                await _applicationContext.AddAsync(mov);
                client.MovimentacaoBancarias.Add(mov);

            }
            await _applicationContext.SaveChangesAsync();
            _logger.LogInformation($"Movimentação bancária cadastrada com sucesso para o cliente {(client.Nome, Formatting.Indented,
                new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                })}  ");
        }
    }
}
