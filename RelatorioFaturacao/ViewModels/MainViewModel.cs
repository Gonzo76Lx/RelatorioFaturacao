using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using RelatorioFaturacao.Models;
using RelatorioFaturacao.Services;

namespace RelatorioFaturacao.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _connectionString = string.Empty;
        private string _searchText = string.Empty;
        private bool _isBusy;
        private string _busyMessage = string.Empty;
        private int _matchCount;
        private bool _isFilterPopupOpen;
        private bool _isSettingsOpen;
        private bool _isInfoOpen;
        private bool _isErrorModalOpen;
        private bool _isLogsModalOpen;
        private string _logContentText = string.Empty;
        private LogItemViewModel? _selectedErrorItem;
        private ColumnFilterInfo? _activeColumnFilter;

        private readonly List<LogItemViewModel> _masterLogs = new();
        private readonly List<ColumnSortInfo> _sortList = new();
        private readonly Dictionary<string, List<LogItemViewModel>> _logsByFileName = new(StringComparer.OrdinalIgnoreCase);
        private ObservableCollection<LogItemViewModel> _displayItems = new();
        private ObservableCollection<LogDisplayGroup> _groupedItems = new();
        private ObservableCollection<GroupChipItem> _activeGroups = new();

        public MainViewModel()
        {
            _connectionString = Preferences.Default.Get("ConnectionString", "");

            SearchCommand = new Command(async () => await ExecuteSearch());
            SelectFileCommand = new Command(async () => await ExecuteSelectFile());
            ExportCommand = new Command(async () => await ExecuteExport());
            ClearSearchCommand = new Command(ClearSearch);
            ClearListCommand = new Command(() => ClearList(keepSearchText: false));

            CopyFileNameCommand = new Command<object>(async (p) => await ExecuteCopyFileName(p));
            CopyRowCommand = new Command<LogItemViewModel>(async (item) => await ExecuteCopyRow(item));

            OpenColumnFilterCommand = new Command<string>(OpenColumnFilter);
            ApplyFilterCommand = new Command(ApplyCurrentFilter);
            ClearFilterCommand = new Command(ClearCurrentFilter);
            CloseFilterPopupCommand = new Command(() => IsFilterPopupOpen = false);

            SortCommand = new Command<string>(col => ToggleSort(col, false));
            ClearSortCommand = new Command(ClearSort);

            AddGroupCommand = new Command<string>(AddGroup);
            RemoveGroupCommand = new Command<GroupChipItem>(RemoveGroup);
            ToggleGroupExpandedCommand = new Command<LogDisplayGroup>(ToggleGroupExpanded);

            OpenSettingsCommand = new Command(() => IsSettingsOpen = true);
            SaveSettingsCommand = new Command(SaveSettings);
            CloseSettingsCommand = new Command(() => IsSettingsOpen = false);

            OpenInfoCommand = new Command(() => IsInfoOpen = true);
            CloseInfoCommand = new Command(() => IsInfoOpen = false);

            OpenLogsCommand = new Command(OpenLogsModal);
            CloseLogsCommand = new Command(() => IsLogsModalOpen = false);
            OpenLogFileCommand = new Command(AppLogger.OpenLogFile);
            OpenLogsFolderCommand = new Command(AppLogger.OpenLogsFolder);
            ClearLogsCommand = new Command(ExecuteClearLogs);
            RefreshLogsCommand = new Command(RefreshLogsText);

            OpenErrorModalCommand = new Command<LogItemViewModel>(OpenErrorModal);
            CloseErrorModalCommand = new Command(() => IsErrorModalOpen = false);
            CopyErrorCommand = new Command(async () => await ExecuteCopyError());
        }

        public string ConnectionString
        {
            get => _connectionString;
            set
            {
                if (SetProperty(ref _connectionString, value))
                {
                    Preferences.Default.Set("ConnectionString", value);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyAllFilters();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set
            {
                if (SetProperty(ref _busyMessage, value))
                {
                    OnPropertyChanged(nameof(HasBusyMessage));
                }
            }
        }

        public bool HasBusyMessage => !string.IsNullOrWhiteSpace(_busyMessage);

        public int MatchCount
        {
            get => _matchCount;
            set => SetProperty(ref _matchCount, value);
        }

        public bool HasGroups => ActiveGroups.Count > 0;
        public bool HasNoGroups => ActiveGroups.Count == 0;

        public bool IsFilterPopupOpen
        {
            get => _isFilterPopupOpen;
            set => SetProperty(ref _isFilterPopupOpen, value);
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }

        public bool IsInfoOpen
        {
            get => _isInfoOpen;
            set => SetProperty(ref _isInfoOpen, value);
        }

        public bool IsLogsModalOpen
        {
            get => _isLogsModalOpen;
            set => SetProperty(ref _isLogsModalOpen, value);
        }

        public string LogContentText
        {
            get => _logContentText;
            set => SetProperty(ref _logContentText, value);
        }

        public bool IsErrorModalOpen
        {
            get => _isErrorModalOpen;
            set => SetProperty(ref _isErrorModalOpen, value);
        }

        public LogItemViewModel? SelectedErrorItem
        {
            get => _selectedErrorItem;
            set => SetProperty(ref _selectedErrorItem, value);
        }

        public ColumnFilterInfo? ActiveColumnFilter
        {
            get => _activeColumnFilter;
            set => SetProperty(ref _activeColumnFilter, value);
        }

        public ObservableCollection<LogItemViewModel> DisplayItems
        {
            get => _displayItems;
            set => SetProperty(ref _displayItems, value);
        }

        public ObservableCollection<LogDisplayGroup> GroupedItems
        {
            get => _groupedItems;
            set => SetProperty(ref _groupedItems, value);
        }

        public ObservableCollection<GroupChipItem> ActiveGroups
        {
            get => _activeGroups;
            set
            {
                if (SetProperty(ref _activeGroups, value))
                {
                    OnPropertyChanged(nameof(HasGroups));
                    OnPropertyChanged(nameof(HasNoGroups));
                }
            }
        }

        public Dictionary<string, ColumnFilterInfo> ColumnFilters { get; } = new()
        {
            ["NomeFicheiro"] = new ColumnFilterInfo { ColumnName = "NomeFicheiro", ColumnTitle = "Ficheiro" },
            ["DataProcessamento"] = new ColumnFilterInfo { ColumnName = "DataProcessamento", ColumnTitle = "Data" },
            ["Estado"] = new ColumnFilterInfo { ColumnName = "Estado", ColumnTitle = "Estado" },
            ["numRetry"] = new ColumnFilterInfo { ColumnName = "numRetry", ColumnTitle = "Tentativas" }
        };

        public ICommand SearchCommand { get; }
        public ICommand SelectFileCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ClearListCommand { get; }
        public ICommand CopyFileNameCommand { get; }
        public ICommand CopyRowCommand { get; }

        public ICommand OpenColumnFilterCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand CloseFilterPopupCommand { get; }

        public ICommand SortCommand { get; }
        public ICommand ClearSortCommand { get; }

        public ICommand AddGroupCommand { get; }
        public ICommand RemoveGroupCommand { get; }
        public ICommand ToggleGroupExpandedCommand { get; }

        public ICommand OpenSettingsCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand CloseSettingsCommand { get; }

        public ICommand OpenInfoCommand { get; }
        public ICommand CloseInfoCommand { get; }

        public ICommand OpenLogsCommand { get; }
        public ICommand CloseLogsCommand { get; }
        public ICommand OpenLogFileCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand RefreshLogsCommand { get; }

        public ICommand OpenErrorModalCommand { get; }
        public ICommand CloseErrorModalCommand { get; }
        public ICommand CopyErrorCommand { get; }

        public string SortNomeFicheiroIndicator => GetSortIndicator("NomeFicheiro");
        public string SortDataIndicator => GetSortIndicator("DataProcessamento");
        public string SortEstadoIndicator => GetSortIndicator("Estado");
        public string SortRetryIndicator => GetSortIndicator("numRetry");

        public bool HasSortNomeFicheiro => IsColumnSorted("NomeFicheiro");
        public bool HasSortData => IsColumnSorted("DataProcessamento");
        public bool HasSortEstado => IsColumnSorted("Estado");
        public bool HasSortRetry => IsColumnSorted("numRetry");

        public Color SortNomeFicheiroHeaderColor => GetHeaderColor("NomeFicheiro");
        public Color SortDataHeaderColor => GetHeaderColor("DataProcessamento");
        public Color SortEstadoHeaderColor => GetHeaderColor("Estado");
        public Color SortRetryHeaderColor => GetHeaderColor("numRetry");

        public bool HasAnySort => _sortList.Any(s => s.Direction != SortDirection.None);

        public string SortSummaryText
        {
            get
            {
                if (_sortList.Count == 0) return string.Empty;
                var list = _sortList.Select(s =>
                {
                    string name = s.ColumnName switch
                    {
                        "NomeFicheiro" => "Ficheiro",
                        "DataProcessamento" => "Data",
                        "Estado" => "Estado",
                        "numRetry" => "Tentativas",
                        _ => s.ColumnName
                    };
                    string dir = s.Direction == SortDirection.Ascending ? "▲" : "▼";
                    return _sortList.Count > 1 ? $"{name} ({dir} {s.SortPriority})" : $"{name} ({dir})";
                });
                return string.Join(", ", list);
            }
        }

        public string MatchSummary
        {
            get
            {
                int current = DisplayItems.Count;
                int total = _masterLogs.Count;
                string baseText = current == total
                    ? $"A mostrar {current} registo(s)"
                    : $"A mostrar {current} de {total} registo(s)";

                if (!string.IsNullOrEmpty(SortSummaryText))
                {
                    baseText += $" • Ordenação: {SortSummaryText}";
                }
                return baseText;
            }
        }

        public void SetMasterLogs(IEnumerable<LogAssinaturaFicheiro> items)
        {
            var sw = Stopwatch.StartNew();
            _masterLogs.Clear();
            _logsByFileName.Clear();

            int estimatedCount = items is ICollection<LogAssinaturaFicheiro> c ? c.Count : 10000;
            var list = new List<LogItemViewModel>(estimatedCount);

            foreach (var item in items)
            {
                var vm = new LogItemViewModel(item);
                list.Add(vm);

                if (!string.IsNullOrEmpty(vm.NomeFicheiro))
                {
                    if (!_logsByFileName.TryGetValue(vm.NomeFicheiro, out var group))
                    {
                        group = new List<LogItemViewModel>(1);
                        _logsByFileName[vm.NomeFicheiro] = group;
                    }
                    group.Add(vm);
                }
            }

            _masterLogs.AddRange(list);

            // Refresh distinct values for column filters
            RefreshFilterDistinctValues();
            ApplyAllFilters();

            AppLogger.LogInfo($"SetMasterLogs: {list.Count} registos carregados e indexados em {sw.ElapsedMilliseconds} ms.");
        }

        private void RefreshFilterDistinctValues()
        {
            foreach (var kvp in ColumnFilters)
            {
                var col = kvp.Key;
                var filterInfo = kvp.Value;

                var distinctStrings = new HashSet<string>();
                foreach (var log in _masterLogs)
                {
                    string val = col switch
                    {
                        "NomeFicheiro" => log.NomeFicheiro,
                        "DataProcessamento" => log.DataProcessamento.ToString("yyyy-MM-dd"),
                        "Estado" => log.EstadoDisplay,
                        "numRetry" => log.numRetry.ToString(),
                        _ => string.Empty
                    };
                    if (!string.IsNullOrEmpty(val))
                    {
                        distinctStrings.Add(val);
                        // Prevent UI freezing from thousands of checkboxes on high cardinality columns
                        if (distinctStrings.Count >= 100)
                            break;
                    }
                }

                filterInfo.DistinctValues.Clear();
                foreach (var str in distinctStrings.OrderBy(s => s).Take(100))
                {
                    filterInfo.DistinctValues.Add(new FilterValueItem
                    {
                        Value = str,
                        DisplayText = str,
                        IsSelected = true
                    });
                }
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        public void ClearList(bool keepSearchText = false)
        {
            _masterLogs.Clear();
            _logsByFileName.Clear();
            _sortList.Clear();
            _selectedErrorItem = null;
            _isErrorModalOpen = false;
            _isFilterPopupOpen = false;
            _activeColumnFilter = null;

            foreach (var filter in ColumnFilters.Values)
            {
                filter.DistinctValues.Clear();
                filter.FilterText = string.Empty;
                filter.FilterOperator = "É igual a";
                filter.IsActive = false;
                filter.SelectAll = true;
            }

            if (!keepSearchText)
            {
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));
            }

            NotifySortPropertiesChanged();

            DisplayItems = new ObservableCollection<LogItemViewModel>();
            GroupedItems = new ObservableCollection<LogDisplayGroup>();
            MatchCount = 0;
            OnPropertyChanged(nameof(MatchSummary));

            AppLogger.LogInfo("Lista de dados e variáveis limpas com sucesso.");
        }

        public async Task ExecuteCopyFileName(object? param)
        {
            string? name = null;
            if (param is LogItemViewModel vm)
            {
                name = vm.NomeFicheiro;
            }
            else if (param is string str)
            {
                name = str;
            }
            else if (param is LogAssinaturaFicheiro model)
            {
                name = model.NomeFicheiro;
            }

            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                await Clipboard.Default.SetTextAsync(name);
                AppLogger.LogInfo($"Nome de ficheiro copiado para a área de transferência: '{name}'");
                await ShowAlertAsync("Copiado", $"O nome do ficheiro foi copiado para a área de transferência:\n\n{name}", "OK");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Erro ao copiar nome do ficheiro", ex);
            }
        }

        public async Task ExecuteCopyRow(LogItemViewModel? item)
        {
            if (item == null) return;
            try
            {
                var text = $"{item.NomeFicheiro}\t{item.DataFormatada}\t{item.EstadoDisplay}\t{item.numRetry}\t{item.MensagemErro}";
                await Clipboard.Default.SetTextAsync(text);
                AppLogger.LogInfo($"Linha copiada para a área de transferência: '{item.NomeFicheiro}'");
                await ShowAlertAsync("Copiado", $"Os dados da linha foram copiados para a área de transferência:\n\n{text}", "OK");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Erro ao copiar linha", ex);
            }
        }

        private void OpenLogsModal()
        {
            RefreshLogsText();
            IsLogsModalOpen = true;
        }

        private void RefreshLogsText()
        {
            LogContentText = AppLogger.GetAllLogsText();
        }

        private void ExecuteClearLogs()
        {
            AppLogger.ClearLogs();
            RefreshLogsText();
        }

        private void OpenColumnFilter(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;
            if (ColumnFilters.TryGetValue(columnName, out var filterInfo))
            {
                ActiveColumnFilter = filterInfo;
                IsFilterPopupOpen = true;
            }
        }

        private void ApplyCurrentFilter()
        {
            if (ActiveColumnFilter != null)
            {
                bool hasUnselectedValues = ActiveColumnFilter.DistinctValues.Any(v => !v.IsSelected);
                bool hasCustomRule = !string.IsNullOrWhiteSpace(ActiveColumnFilter.FilterText);
                ActiveColumnFilter.IsActive = hasUnselectedValues || hasCustomRule;
            }

            IsFilterPopupOpen = false;
            ApplyAllFilters();
        }

        private void ClearCurrentFilter()
        {
            if (ActiveColumnFilter != null)
            {
                ActiveColumnFilter.SelectAll = true;
                ActiveColumnFilter.FilterText = string.Empty;
                ActiveColumnFilter.FilterOperator = "É igual a";
                ActiveColumnFilter.IsActive = false;
            }

            IsFilterPopupOpen = false;
            ApplyAllFilters();
        }

        public void ToggleSort(string? columnName, bool isMultiSort = false)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return;

            if (!isMultiSort)
            {
                // Single column sort
                var existing = _sortList.FirstOrDefault(s => s.ColumnName == columnName);
                if (existing != null && _sortList.Count == 1)
                {
                    // Cycle: Ascending -> Descending -> None
                    if (existing.Direction == SortDirection.Ascending)
                    {
                        existing.Direction = SortDirection.Descending;
                    }
                    else if (existing.Direction == SortDirection.Descending)
                    {
                        _sortList.Clear();
                    }
                }
                else
                {
                    _sortList.Clear();
                    _sortList.Add(new ColumnSortInfo
                    {
                        ColumnName = columnName,
                        Direction = SortDirection.Ascending,
                        SortPriority = 1
                    });
                }
            }
            else
            {
                // Multi column sort (Shift + Click / Ctrl + Click)
                var existing = _sortList.FirstOrDefault(s => s.ColumnName == columnName);
                if (existing == null)
                {
                    _sortList.Add(new ColumnSortInfo
                    {
                        ColumnName = columnName,
                        Direction = SortDirection.Ascending,
                        SortPriority = _sortList.Count + 1
                    });
                }
                else if (existing.Direction == SortDirection.Ascending)
                {
                    existing.Direction = SortDirection.Descending;
                }
                else if (existing.Direction == SortDirection.Descending)
                {
                    _sortList.Remove(existing);
                    // Reindex priorities
                    for (int i = 0; i < _sortList.Count; i++)
                    {
                        _sortList[i].SortPriority = i + 1;
                    }
                }
            }

            NotifySortPropertiesChanged();
            ApplyAllFilters();
        }

        public void ClearSort()
        {
            _sortList.Clear();
            NotifySortPropertiesChanged();
            ApplyAllFilters();
        }

        public bool IsColumnSorted(string columnName)
        {
            return _sortList.Any(s => s.ColumnName == columnName && s.Direction != SortDirection.None);
        }

        public string GetSortIndicator(string columnName)
        {
            var sort = _sortList.FirstOrDefault(s => s.ColumnName == columnName);
            if (sort == null || sort.Direction == SortDirection.None)
                return string.Empty;

            string arrow = sort.Direction == SortDirection.Ascending ? "▲" : "▼";
            if (_sortList.Count > 1)
            {
                return $"{arrow} {sort.SortPriority}";
            }
            return arrow;
        }

        private Color GetHeaderColor(string columnName)
        {
            bool isSorted = IsColumnSorted(columnName);
            if (!isSorted) return Color.FromArgb("#334155");
            if (Application.Current?.Resources != null &&
                Application.Current.Resources.TryGetValue("Primary", out var res) &&
                res is Color c)
            {
                return c;
            }
            return Color.FromArgb("#6841CA");
        }

        private void NotifySortPropertiesChanged()
        {
            OnPropertyChanged(nameof(SortNomeFicheiroIndicator));
            OnPropertyChanged(nameof(SortDataIndicator));
            OnPropertyChanged(nameof(SortEstadoIndicator));
            OnPropertyChanged(nameof(SortRetryIndicator));

            OnPropertyChanged(nameof(HasSortNomeFicheiro));
            OnPropertyChanged(nameof(HasSortData));
            OnPropertyChanged(nameof(HasSortEstado));
            OnPropertyChanged(nameof(HasSortRetry));

            OnPropertyChanged(nameof(SortNomeFicheiroHeaderColor));
            OnPropertyChanged(nameof(SortDataHeaderColor));
            OnPropertyChanged(nameof(SortEstadoHeaderColor));
            OnPropertyChanged(nameof(SortRetryHeaderColor));

            OnPropertyChanged(nameof(HasAnySort));
            OnPropertyChanged(nameof(SortSummaryText));
            OnPropertyChanged(nameof(MatchSummary));
        }

        private List<LogItemViewModel> ApplySorting(List<LogItemViewModel> items)
        {
            if (_sortList.Count == 0 || items.Count <= 1) return items;

            items.Sort((a, b) =>
            {
                for (int i = 0; i < _sortList.Count; i++)
                {
                    var sort = _sortList[i];
                    if (sort.Direction == SortDirection.None) continue;

                    int cmp = sort.ColumnName switch
                    {
                        "NomeFicheiro" => string.Compare(a.NomeFicheiro, b.NomeFicheiro, StringComparison.CurrentCultureIgnoreCase),
                        "DataProcessamento" => a.DataProcessamento.CompareTo(b.DataProcessamento),
                        "Estado" => string.Compare(a.EstadoDisplay, b.EstadoDisplay, StringComparison.CurrentCultureIgnoreCase),
                        "numRetry" => a.numRetry.CompareTo(b.numRetry),
                        _ => 0
                    };

                    if (cmp != 0)
                    {
                        return sort.Direction == SortDirection.Ascending ? cmp : -cmp;
                    }
                }
                return 0;
            });

            return items;
        }

        public void AddGroup(string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return;
            if (ActiveGroups.Any(g => g.ColumnName == columnName)) return;

            string title = columnName switch
            {
                "DataProcessamento" => "Data",
                "Estado" => "Estado",
                "numRetry" => "Tentativas",
                "NomeFicheiro" => "Ficheiro",
                _ => columnName
            };

            ActiveGroups.Add(new GroupChipItem { ColumnName = columnName, Title = title });
            OnPropertyChanged(nameof(HasGroups));
            OnPropertyChanged(nameof(HasNoGroups));
            ApplyAllFilters();
        }

        public void RemoveGroup(GroupChipItem? group)
        {
            if (group != null && ActiveGroups.Contains(group))
            {
                ActiveGroups.Remove(group);
                OnPropertyChanged(nameof(HasGroups));
                OnPropertyChanged(nameof(HasNoGroups));
                ApplyAllFilters();
            }
        }

        private void ToggleGroupExpanded(LogDisplayGroup? group)
        {
            if (group != null)
            {
                group.IsExpanded = !group.IsExpanded;
            }
        }

        private async void SaveSettings()
        {
            Preferences.Default.Set("ConnectionString", ConnectionString);
            IsSettingsOpen = false;
            await ShowAlertAsync("Definições", "Connection String guardada com sucesso.", "OK");
        }

        private void ApplyAllFilters()
        {
            var sw = Stopwatch.StartNew();
            var search = SearchText?.Trim() ?? string.Empty;
            var hasSearch = !string.IsNullOrWhiteSpace(search);
            var activeFilters = ColumnFilters.Values.Where(f => f.IsActive).ToList();
            var hasActiveFilters = activeFilters.Count > 0;

            List<LogItemViewModel> filtered;

            if (!hasSearch && !hasActiveFilters)
            {
                filtered = new List<LogItemViewModel>(_masterLogs);
                MatchCount = _masterLogs.Count;
            }
            else
            {
                filtered = new List<LogItemViewModel>(_masterLogs.Count);
                int matchCount = 0;

                foreach (var item in _masterLogs)
                {
                    if (hasSearch && !item.MatchesSearch(search))
                        continue;

                    bool passesColumnFilters = true;
                    for (int f = 0; f < activeFilters.Count; f++)
                    {
                        var filter = activeFilters[f];
                        string cellValue = filter.ColumnName switch
                        {
                            "NomeFicheiro" => item.NomeFicheiro,
                            "DataProcessamento" => item.DataProcessamento.ToString("yyyy-MM-dd"),
                            "Estado" => item.EstadoDisplay,
                            "numRetry" => item.numRetry.ToString(),
                            _ => string.Empty
                        };

                        // Check distinct values checklist
                        var checkedItem = filter.DistinctValues.FirstOrDefault(v => v.Value == cellValue);
                        if (checkedItem != null && !checkedItem.IsSelected)
                        {
                            passesColumnFilters = false;
                            break;
                        }

                        // Check custom criteria
                        if (!string.IsNullOrWhiteSpace(filter.FilterText))
                        {
                            var comp = filter.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                            bool rulePass = filter.FilterOperator switch
                            {
                                "É igual a" => string.Equals(cellValue, filter.FilterText, comp),
                                "Contém" => cellValue.Contains(filter.FilterText, comp),
                                "Começa com" => cellValue.StartsWith(filter.FilterText, comp),
                                "Termina com" => cellValue.EndsWith(filter.FilterText, comp),
                                "Não contém" => !cellValue.Contains(filter.FilterText, comp),
                                _ => true
                            };

                            if (!rulePass)
                            {
                                passesColumnFilters = false;
                                break;
                            }
                        }
                    }

                    if (passesColumnFilters)
                    {
                        if (hasSearch)
                        {
                            item.UpdateHighlight(search);
                        }
                        filtered.Add(item);
                        matchCount++;
                    }
                }

                MatchCount = matchCount;
            }

            filtered = ApplySorting(filtered);

            DisplayItems = new ObservableCollection<LogItemViewModel>(filtered);
            OnPropertyChanged(nameof(MatchSummary));

            // Grouping logic
            if (ActiveGroups.Count > 0)
            {
                var primaryGroup = ActiveGroups[0];
                var groups = filtered
                    .GroupBy(x => GetGroupKey(x, primaryGroup.ColumnName))
                    .OrderBy(g => g.Key)
                    .Select(g => new LogDisplayGroup(
                        groupTitle: $"{primaryGroup.Title}: {g.Key}",
                        groupKey: g.Key,
                        items: g
                    ));

                GroupedItems = new ObservableCollection<LogDisplayGroup>(groups);
            }
            else
            {
                GroupedItems = new ObservableCollection<LogDisplayGroup>
                {
                    new LogDisplayGroup("", "All", filtered)
                };
            }

            AppLogger.LogDebug($"ApplyAllFilters concluído em {sw.ElapsedMilliseconds} ms. Total filtrado: {filtered.Count}.");
        }

        private string GetGroupKey(LogItemViewModel item, string col)
        {
            return col switch
            {
                "DataProcessamento" => item.DataProcessamento.ToString("yyyy-MM-dd"),
                "Estado" => item.EstadoDisplay,
                "numRetry" => $"Retry {item.numRetry}",
                "NomeFicheiro" => item.NomeFicheiro.Length > 0 ? item.NomeFicheiro[0].ToString().ToUpper() : "-",
                _ => "-"
            };
        }

        private void OpenErrorModal(LogItemViewModel? item)
        {
            if (item != null && item.IsErro)
            {
                SelectedErrorItem = item;
                IsErrorModalOpen = true;
            }
        }

        private async Task ExecuteCopyError()
        {
            if (SelectedErrorItem == null) return;
            var text = $"Ficheiro: {SelectedErrorItem.NomeFicheiro}\nData: {SelectedErrorItem.DataFormatada}\nEstado: {SelectedErrorItem.EstadoDisplay}\nTentativas: {SelectedErrorItem.numRetry}\n\nDetalhe do Erro:\n{SelectedErrorItem.MensagemErro}";
            await Clipboard.Default.SetTextAsync(text);
            await ShowAlertAsync("Copiado", "O detalhe do erro foi copiado para a área de transferência.", "OK");
        }

        private const int LargeRecordCountThreshold = 5000;

        private async Task ExecuteSearch()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                AppLogger.LogWarning("Tentativa de pesquisa sem Connection String configurada.");
                await ShowAlertAsync("Aviso", "Por favor, defina a Connection String nas definições (ícone ⚙).", "OK");
                return;
            }

            IsBusy = true;
            BusyMessage = "A verificar quantidade de registos na base de dados...";
            try
            {
                var searchTrimmed = SearchText?.Trim() ?? string.Empty;
                string countQuery;
                string query;
                string searchParam = $"%{searchTrimmed}%";

                if (string.IsNullOrWhiteSpace(searchTrimmed))
                {
                    countQuery = "SELECT COUNT(*) FROM FaturacaoEletronica.dbo.LogsAssinaturaFicheiros WITH (NOLOCK)";
                    query = "SELECT * FROM FaturacaoEletronica.dbo.LogsAssinaturaFicheiros WITH (NOLOCK) ORDER BY DataProcessamento DESC";
                    AppLogger.LogInfo("A preparar pesquisa geral (sem limite de TOP)...");
                }
                else
                {
                    countQuery = "SELECT COUNT(*) FROM FaturacaoEletronica.dbo.LogsAssinaturaFicheiros WITH (NOLOCK) WHERE NomeFicheiro LIKE @search";
                    query = "SELECT * FROM FaturacaoEletronica.dbo.LogsAssinaturaFicheiros WITH (NOLOCK) WHERE NomeFicheiro LIKE @search ORDER BY DataProcessamento DESC";
                    AppLogger.LogInfo($"A preparar pesquisa filtrada por '{searchTrimmed}' (sem limite de TOP)...");
                }

                int totalCount = await Task.Run(async () => await FetchCount(countQuery, searchParam));
                AppLogger.LogInfo($"Total de registos encontrados na BD: {totalCount}.");

                if (totalCount > LargeRecordCountThreshold)
                {
                    IsBusy = false;
                    bool proceed = await ShowConfirmationAsync(
                        "Volume Elevado de Registos",
                        $"A pesquisa encontrou {totalCount:N0} registos na base de dados.\n\nCarregar um volume elevado de dados pode demorar alguns momentos e consumir mais memória.\n\nPretende continuar e carregar todos os registos?",
                        "Continuar",
                        "Cancelar");

                    if (!proceed)
                    {
                        AppLogger.LogInfo($"Pesquisa cancelada pelo utilizador após aviso ({totalCount} registos).");
                        return;
                    }
                }

                // Limpa sempre a lista e repõe variáveis antes de iniciar nova pesquisa
                ClearList(keepSearchText: true);

                IsBusy = true;
                BusyMessage = totalCount > 0
                    ? $"A carregar {totalCount:N0} registos da base de dados..."
                    : "A carregar registos da base de dados...";

                var items = await Task.Run(async () => await FetchLogs(query, searchParam));
                AppLogger.LogInfo($"Pesquisa devolveu {items.Count} registos.");
                SetMasterLogs(items);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Erro em ExecuteSearch", ex);
                await ShowAlertAsync("Erro", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task ExecuteSelectFile()
        {
            try
            {
                AppLogger.LogInfo("A abrir seletor de ficheiros...");
                var result = await FilePicker.Default.PickAsync();
                if (result == null)
                {
                    AppLogger.LogInfo("Seleção de ficheiro cancelada pelo utilizador.");
                    return;
                }

                // Limpa sempre a lista e variáveis anteriores antes de carregar o novo ficheiro
                ClearList(keepSearchText: false);

                AppLogger.LogInfo($"Ficheiro selecionado: '{result.FullPath}' (Nome: '{result.FileName}')");
                IsBusy = true;
                BusyMessage = "A ler ficheiro de entrada...";

                await Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    var lines = await File.ReadAllLinesAsync(result.FullPath);
                    var rawLines = lines
                        .Select(l => l.Trim().Trim('"', '\''))
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    AppLogger.LogInfo($"Ficheiro lido: {lines.Length} linhas totais, {rawLines.Count} ficheiros únicos a processar. Tempo de leitura: {sw.ElapsedMilliseconds} ms.");

                    if (rawLines.Count == 0)
                    {
                        AppLogger.LogWarning("O ficheiro selecionado não continha linhas válidas.");
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await ShowAlertAsync("Aviso", "O ficheiro selecionado está vazio ou não contém nomes de ficheiro válidos.", "OK");
                        });
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(ConnectionString))
                    {
                        AppLogger.LogWarning("Connection String não definida. A gerar simulação offline com nomes completos.");
                        var offlineList = rawLines.Select((line, index) =>
                        {
                            bool isError = index % 4 == 0;
                            return new LogAssinaturaFicheiro
                            {
                                NomeFicheiro = line, // Nome completo
                                DataProcessamento = DateTime.Now.AddMinutes(-index * 15),
                                Estado = isError ? "Erro" : "OK",
                                numRetry = isError ? 2 : 0,
                                MensagemErro = isError
                                    ? $"Falha no processamento do ficheiro '{line}': Falha de comunicação com o serviço de assinatura eletrónica (Tentativa {2} de 3). Código de erro: AT_SIGN_504."
                                    : null
                            };
                        }).ToList();

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            SetMasterLogs(offlineList);
                        });
                        return;
                    }

                    // Cada linha do ficheiro é um termo de pesquisa LIKE '%linha%'
                    var searchList = rawLines;
                    AppLogger.LogInfo($"Linhas a pesquisar (LIKE '%termo%'): {searchList.Count} termos únicos.");

                    var dbLogsByMatchKey = new ConcurrentDictionary<string, List<LogAssinaturaFicheiro>>(StringComparer.OrdinalIgnoreCase);
                    var querySw = Stopwatch.StartNew();

                    const int batchSize = 50;
                    var batches = new List<List<string>>();
                    for (int i = 0; i < searchList.Count; i += batchSize)
                    {
                        batches.Add(searchList.Skip(i).Take(batchSize).ToList());
                    }

                    int totalBatches = batches.Count;
                    int completedBatches = 0;
                    int totalRowsFetched = 0;

                    AppLogger.LogInfo($"Início de consultas parciais (LIKE) paralelas à base de dados ({totalBatches} lotes de até {batchSize} termos)...");

                    //Vamos definir o número de threads
                    var parallelOptions = new ParallelOptions
                    {
                        //Escolhe o numero dethreads entre 4 e 8, baseado no numero de processadores, sendo que no minimo são 4 threads e no máximo 8 threads
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 4, 8)
                    };

                    await Parallel.ForEachAsync(batches.Select((b, idx) => (Batch: b, Index: idx + 1)), parallelOptions, async (item, ct) =>
                    {
                        var batch = item.Batch;
                        var batchIndex = item.Index;
                        var batchSw = Stopwatch.StartNew();

                        try
                        {
                            using var conn = new SqlConnection(ConnectionString);
                            await conn.OpenAsync(ct);

                            using var cmd = new SqlCommand();
                            cmd.Connection = conn;
                            cmd.CommandTimeout = 300; // 5 minutes

                            var conditions = new List<string>(batch.Count);
                            for (int j = 0; j < batch.Count; j++)
                            {
                                var paramName = $"@p{j}";
                                conditions.Add($"NomeFicheiro LIKE {paramName}");
                                var escapedTerm = batch[j]
                                    .Replace("[", "[[]")
                                    .Replace("%", "[%]")
                                    .Replace("_", "[_]");
                                cmd.Parameters.AddWithValue(paramName, $"%{escapedTerm}%");
                            }
                            
                            string commandText=$"SELECT * FROM FaturacaoEletronica.dbo.LogsAssinaturaFicheiros WITH (NOLOCK) WHERE ({string.Join(" OR ", conditions)}) ORDER BY DataProcessamento DESC";
                            cmd.CommandText = commandText;

                            int batchRows = 0;
                            using var reader = await cmd.ExecuteReaderAsync(ct);
                            while (await reader.ReadAsync(ct))
                            {
                                var logItem = ReadLogFromReader(reader);
                                batchRows++;
                                Interlocked.Increment(ref totalRowsFetched);

                                if (!string.IsNullOrEmpty(logItem.NomeFicheiro))
                                {
                                    for (int t = 0; t < batch.Count; t++)
                                    {
                                        var term = batch[t];
                                        if (logItem.NomeFicheiro.Contains(term, StringComparison.OrdinalIgnoreCase))
                                        {
                                            var list = dbLogsByMatchKey.GetOrAdd(term, _ => new List<LogAssinaturaFicheiro>());
                                            lock (list)
                                            {
                                                if (!list.Any(x => x.NomeFicheiro == logItem.NomeFicheiro && x.DataProcessamento == logItem.DataProcessamento))
                                                {
                                                    list.Add(logItem);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            int done = Interlocked.Increment(ref completedBatches);
                            if (done % 5 == 0 || done == totalBatches)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    BusyMessage = $"A consultar base de dados... Lote {done} de {totalBatches} ({totalRowsFetched} registos obtidos)";
                                });
                            }

                            AppLogger.LogInfo($"Lote {batchIndex}/{totalBatches} concluído em {batchSw.ElapsedMilliseconds} ms. Linhas obtidas: {batchRows}.");
                        }
                        catch (Exception batchEx)
                        {
                            AppLogger.LogError($"Erro ao executar lote {batchIndex}/{totalBatches}", batchEx);
                        }
                    });

                    AppLogger.LogInfo($"Todas as consultas SQL concorrentes concluídas em {querySw.ElapsedMilliseconds} ms. Total de registos obtidos da BD: {totalRowsFetched}.");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        BusyMessage = "A estruturar resultados com nomes completos...";
                    });
                    AppLogger.LogInfo("A mapear registos da BD para os ficheiros originais...");

                    var finalResults = new List<LogAssinaturaFicheiro>(rawLines.Count);
                    int foundCount = 0;
                    int notFoundCount = 0;

                    foreach (var rawLine in rawLines)
                    {
                        if (dbLogsByMatchKey.TryGetValue(rawLine, out var matchedLogs) && matchedLogs.Count > 0)
                        {
                            foundCount++;
                            foreach (var m in matchedLogs)
                            {
                                finalResults.Add(new LogAssinaturaFicheiro
                                {
                                    NomeFicheiro = m.NomeFicheiro, // Nome completo retornado da BD
                                    DataProcessamento = m.DataProcessamento,
                                    Estado = m.Estado,
                                    numRetry = m.numRetry,
                                    MensagemErro = m.MensagemErro
                                });
                            }
                        }
                        else
                        {
                            notFoundCount++;
                            finalResults.Add(new LogAssinaturaFicheiro
                            {
                                NomeFicheiro = rawLine,
                                DataProcessamento = DateTime.Now,
                                Estado = "Não Encontrado",
                                numRetry = 0,
                                MensagemErro = $"Nenhum registo contendo '{rawLine}' foi encontrado na base de dados de registos de faturação."
                            });
                        }
                    }

                    AppLogger.LogInfo($"Mapeamento concluído: {rawLines.Count} ficheiros de entrada -> {finalResults.Count} linhas totais no relatório. Encontrados: {foundCount}, Não Encontrados: {notFoundCount}. Tempo total: {sw.ElapsedMilliseconds} ms.");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetMasterLogs(finalResults);
                    });
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Erro global em ExecuteSelectFile", ex);
                await ShowAlertAsync("Erro", $"Ocorreu um erro ao processar os ficheiros: {ex.Message}\nConsulte os logs para mais detalhes.", "OK");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task<List<LogAssinaturaFicheiro>> FetchLogs(string query, string searchParam)
        {
            var list = new List<LogAssinaturaFicheiro>();
            var sw = Stopwatch.StartNew();

            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.CommandTimeout = 300; // 5 minutes timeout
            if (query.Contains("@search"))
            {
                cmd.Parameters.AddWithValue("@search", searchParam);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(ReadLogFromReader(reader));
            }

            AppLogger.LogInfo($"FetchLogs executado em {sw.ElapsedMilliseconds} ms. {list.Count} linhas retornadas.");
            return list;
        }

        private async Task<int> FetchCount(string query, string searchParam)
        {
            var sw = Stopwatch.StartNew();
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.CommandTimeout = 300; // 5 minutes timeout
            if (query.Contains("@search"))
            {
                cmd.Parameters.AddWithValue("@search", searchParam);
            }

            var result = await cmd.ExecuteScalarAsync();
            int count = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            AppLogger.LogInfo($"FetchCount executado em {sw.ElapsedMilliseconds} ms. Contagem: {count} registos.");
            return count;
        }

        private static LogAssinaturaFicheiro ReadLogFromReader(SqlDataReader reader)
        {
            var item = new LogAssinaturaFicheiro();

            var rawNome = reader["NomeFicheiro"];
            if (rawNome != null && rawNome != DBNull.Value)
            {
                item.NomeFicheiro = rawNome.ToString() ?? string.Empty;
            }

            var rawData = reader["DataProcessamento"];
            if (rawData != null && rawData != DBNull.Value)
            {
                if (rawData is DateTime dt)
                {
                    item.DataProcessamento = dt;
                }
                else if (DateTime.TryParse(rawData.ToString(), out var parsedDt))
                {
                    item.DataProcessamento = parsedDt;
                }
            }

            var rawEstado = reader["Estado"];
            if (rawEstado != null && rawEstado != DBNull.Value)
            {
                item.Estado = rawEstado.ToString() ?? string.Empty;
            }

            var rawRetry = reader["numRetry"];
            if (rawRetry != null && rawRetry != DBNull.Value)
            {
                if (rawRetry is int intRetry)
                {
                    item.numRetry = intRetry;
                }
                else if (int.TryParse(rawRetry.ToString(), out var parsedRetry))
                {
                    item.numRetry = parsedRetry;
                }
            }

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var col = reader.GetName(i);
                if (col.Equals("MensagemErro", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("Erro", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("DescricaoErro", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("DetalheErro", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("Observacoes", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("Log", StringComparison.OrdinalIgnoreCase))
                {
                    var val = reader.GetValue(i);
                    if (val != null && val != DBNull.Value)
                    {
                        item.MensagemErro = val.ToString();
                        break;
                    }
                }
            }

            return item;
        }

        private async Task ExecuteExport()
        {
            if (DisplayItems.Count == 0)
            {
                AppLogger.LogWarning("Tentativa de exportação sem dados na grelha.");
                await ShowAlertAsync("Info", "Não há dados para exportar.", "OK");
                return;
            }

            IsBusy = true;
            BusyMessage = "A gerar ficheiro Excel com nomes completos...";
            try
            {
                AppLogger.LogInfo($"Início da exportação Excel para {DisplayItems.Count} registos.");
                var sw = Stopwatch.StartNew();

                var fileName = $"Relatorio_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string filePath;

#if WINDOWS
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("Excel Workbook", new List<string>() { ".xlsx" });
                picker.SuggestedFileName = fileName;

                if (App.Current?.Windows.Count > 0 && App.Current.Windows[0].Handler?.PlatformView is MauiWinUIWindow win)
                {
                    var hwnd = win.WindowHandle;
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSaveFileAsync();
                if (file == null)
                {
                    AppLogger.LogInfo("Exportação Excel cancelada pelo utilizador.");
                    return;
                }
                filePath = file.Path;
#else
                filePath = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
#endif

                var itemsToExport = DisplayItems.ToList();

                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Logs");

                    // Headers styled
                    worksheet.Cell(1, 1).Value = "NomeFicheiro";
                    worksheet.Cell(1, 2).Value = "DataProcessamento";
                    worksheet.Cell(1, 3).Value = "Estado";
                    worksheet.Cell(1, 4).Value = "numRetry";

                    var headerRow = worksheet.Row(1);
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(104, 65, 202);
                    headerRow.Style.Font.FontColor = XLColor.White;

                    for (int i = 0; i < itemsToExport.Count; i++)
                    {
                        var item = itemsToExport[i];
                        int row = i + 2;
                        // NOME COMPLETO exportado na coluna 1
                        worksheet.Cell(row, 1).Value = item.NomeFicheiro;
                        worksheet.Cell(row, 2).Value = item.DataProcessamento;
                        worksheet.Cell(row, 3).Value = item.Estado;
                        worksheet.Cell(row, 4).Value = item.numRetry;
                    }

                    if (itemsToExport.Count <= 2000)
                    {
                        worksheet.Columns().AdjustToContents(1, 120);
                    }
                    else
                    {
                        worksheet.Column(1).Width = 60;
                        worksheet.Column(2).Width = 22;
                        worksheet.Column(3).Width = 18;
                        worksheet.Column(4).Width = 12;
                    }

                    workbook.SaveAs(filePath);
                });

                AppLogger.LogInfo($"Ficheiro Excel gravado com sucesso em '{filePath}' em {sw.ElapsedMilliseconds} ms.");
                await ShowAlertAsync("Sucesso", $"Relatório guardado com sucesso em: {filePath}", "OK");

                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Erro na exportação Excel", ex);
                await ShowAlertAsync("Erro", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task ShowAlertAsync(string title, string message, string cancel)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
            {
                await Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
            }
        }

        private async Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
            {
                return await Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel);
            }
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
