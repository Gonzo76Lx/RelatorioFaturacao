using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RelatorioFaturacao.Models
{
    public enum SortDirection
    {
        None,
        Ascending,
        Descending
    }

    public class ColumnSortInfo : INotifyPropertyChanged
    {
        private string _columnName = string.Empty;
        private SortDirection _direction = SortDirection.None;
        private int _sortPriority = 0;

        public string ColumnName
        {
            get => _columnName;
            set => SetProperty(ref _columnName, value);
        }

        public SortDirection Direction
        {
            get => _direction;
            set
            {
                if (SetProperty(ref _direction, value))
                {
                    OnPropertyChanged(nameof(IsSorted));
                    OnPropertyChanged(nameof(IsAscending));
                    OnPropertyChanged(nameof(IsDescending));
                }
            }
        }

        public int SortPriority
        {
            get => _sortPriority;
            set => SetProperty(ref _sortPriority, value);
        }

        public bool IsSorted => Direction != SortDirection.None;
        public bool IsAscending => Direction == SortDirection.Ascending;
        public bool IsDescending => Direction == SortDirection.Descending;

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
