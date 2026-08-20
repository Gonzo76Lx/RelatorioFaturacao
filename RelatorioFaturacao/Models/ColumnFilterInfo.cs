using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RelatorioFaturacao.Models
{
    public class FilterValueItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private string _value = string.Empty;
        private string _displayText = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class ColumnFilterInfo : INotifyPropertyChanged
    {
        private string _columnName = string.Empty;
        private string _columnTitle = string.Empty;
        private bool _selectAll = true;
        private string _filterOperator = "É igual a";
        private string _filterText = string.Empty;
        private bool _isCaseSensitive;
        private string _condition = "E";
        private bool _isActive;
        private ObservableCollection<FilterValueItem> _distinctValues = new();

        public string ColumnName
        {
            get => _columnName;
            set => SetProperty(ref _columnName, value);
        }

        public string ColumnTitle
        {
            get => _columnTitle;
            set => SetProperty(ref _columnTitle, value);
        }

        public bool SelectAll
        {
            get => _selectAll;
            set
            {
                if (SetProperty(ref _selectAll, value))
                {
                    foreach (var item in DistinctValues)
                    {
                        item.IsSelected = value;
                    }
                }
            }
        }

        public string FilterOperator
        {
            get => _filterOperator;
            set => SetProperty(ref _filterOperator, value);
        }

        public string FilterText
        {
            get => _filterText;
            set => SetProperty(ref _filterText, value);
        }

        public bool IsCaseSensitive
        {
            get => _isCaseSensitive;
            set => SetProperty(ref _isCaseSensitive, value);
        }

        public string Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ObservableCollection<FilterValueItem> DistinctValues
        {
            get => _distinctValues;
            set => SetProperty(ref _distinctValues, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
