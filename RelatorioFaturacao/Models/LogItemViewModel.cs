using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace RelatorioFaturacao.Models
{
    public class GroupChipItem : INotifyPropertyChanged
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class LogItemViewModel : INotifyPropertyChanged
    {
        private readonly LogAssinaturaFicheiro _model;
        private string _currentSearch = string.Empty;
        private bool _isMatched;
        private bool _isHighlighted;
        private FormattedString? _nomeFicheiroFormatted;

        public LogItemViewModel(LogAssinaturaFicheiro model)
        {
            _model = model;
        }

        public LogAssinaturaFicheiro Model => _model;

        public string NomeFicheiro => _model.NomeFicheiro ?? string.Empty;
        public DateTime DataProcessamento => _model.DataProcessamento;
        public string Estado => _model.Estado ?? string.Empty;

        public string EstadoDisplay => string.IsNullOrWhiteSpace(Estado) ? (IsOk ? "OK" : "Erro") : Estado;

        public string EstadoButtonText => IsErro ? $"⚠ {EstadoDisplay}" : $"✓ {EstadoDisplay}";

        public int numRetry => _model.numRetry;
        public string MensagemErro
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_model.MensagemErro))
                    return _model.MensagemErro;

                if (IsErro && !string.IsNullOrWhiteSpace(_model.Estado))
                    return _model.Estado;

                if (IsErro)
                    return $"O ficheiro '{NomeFicheiro}' falhou durante o processamento/assinatura com estado '{EstadoDisplay}' após {numRetry} tentativa(s).";

                return string.Empty;
            }
        }

        public bool HasMensagemErro => IsErro && !string.IsNullOrWhiteSpace(MensagemErro);

        public string DataFormatada => _model.DataProcessamento.ToString("g");

        public bool IsOk => DetermineIsOk(Estado);
        public bool IsErro => !IsOk;

        public static bool DetermineIsOk(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;
            var s = estado.Trim();

            if (s.StartsWith("Rejeit", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Err", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Fail", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Falh", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Inv", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Canc", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Não Encontrado", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Nao Encontrado", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public bool IsMatched
        {
            get => _isMatched;
            set => SetProperty(ref _isMatched, value);
        }

        public FormattedString NomeFicheiroFormatted
        {
            get
            {
                if (_nomeFicheiroFormatted == null)
                {
                    _nomeFicheiroFormatted = BuildFormattedString(_currentSearch);
                }
                return _nomeFicheiroFormatted;
            }
            private set => SetProperty(ref _nomeFicheiroFormatted, value);
        }

        public Color RowBackground => _isMatched && !string.IsNullOrWhiteSpace(_currentSearch)
            ? Color.FromArgb("#FEF6D8")
            : Colors.Transparent;

        public bool MatchesSearch(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;

            return NomeFicheiro.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   Estado.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   EstadoDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   DataFormatada.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   numRetry.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   MensagemErro.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        public void UpdateHighlight(string searchText)
        {
            var newSearch = searchText?.Trim() ?? string.Empty;
            if (_isHighlighted && string.Equals(_currentSearch, newSearch, StringComparison.Ordinal))
            {
                return;
            }

            _currentSearch = newSearch;
            _isHighlighted = true;
            _nomeFicheiroFormatted = null; // Re-evaluate lazily

            if (string.IsNullOrWhiteSpace(_currentSearch))
            {
                IsMatched = false;
            }
            else
            {
                IsMatched = MatchesSearch(_currentSearch);
            }

            OnPropertyChanged(nameof(NomeFicheiroFormatted));
            OnPropertyChanged(nameof(RowBackground));
        }

        private FormattedString BuildFormattedString(string search)
        {
            var text = NomeFicheiro;
            var normalColor = Color.FromArgb("#0F172A");
            var highlightColor = Color.FromArgb("#C5221F");

            var fs = new FormattedString();
            if (string.IsNullOrEmpty(text))
            {
                return fs;
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                fs.Spans.Add(new Span { Text = text, TextColor = normalColor });
                return fs;
            }

            int startIndex = 0;
            while (startIndex < text.Length)
            {
                int index = text.IndexOf(search, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    fs.Spans.Add(new Span { Text = text.Substring(startIndex), TextColor = normalColor });
                    break;
                }

                if (index > startIndex)
                {
                    fs.Spans.Add(new Span { Text = text.Substring(startIndex, index - startIndex), TextColor = normalColor });
                }

                fs.Spans.Add(new Span
                {
                    Text = text.Substring(index, search.Length),
                    TextColor = highlightColor,
                    FontAttributes = FontAttributes.Bold
                });

                startIndex = index + search.Length;
            }

            return fs;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
