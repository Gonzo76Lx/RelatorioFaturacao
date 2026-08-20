using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RelatorioFaturacao.Models
{
    public class LogDisplayGroup : ObservableCollection<LogItemViewModel>, INotifyPropertyChanged
    {
        private string _groupTitle = string.Empty;
        private string _groupKey = string.Empty;
        private bool _isExpanded = true;
        private int _totalCount;

        public LogDisplayGroup(string groupTitle, string groupKey, IEnumerable<LogItemViewModel> items) : base(items)
        {
            _groupTitle = groupTitle;
            _groupKey = groupKey;
            _totalCount = this.Count;
        }

        public string GroupTitle
        {
            get => _groupTitle;
            set => SetProperty(ref _groupTitle, value);
        }

        public string GroupKey
        {
            get => _groupKey;
            set => SetProperty(ref _groupKey, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(ChevronIcon)));
                }
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public string ChevronIcon => _isExpanded ? "∨" : "›";

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
