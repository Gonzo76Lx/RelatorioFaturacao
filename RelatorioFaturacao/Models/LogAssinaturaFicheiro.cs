using System;

namespace RelatorioFaturacao.Models
{
    public class LogAssinaturaFicheiro
    {
        public string? NomeFicheiro { get; set; }
        public DateTime DataProcessamento { get; set; }
        public string? Estado { get; set; }
        public int numRetry { get; set; }
        public string? MensagemErro { get; set; }
    }
}
