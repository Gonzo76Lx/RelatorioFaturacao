using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RelatorioFaturacao.ViewModels;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace RelatorioFaturacao
{
    public partial class MainPage : ContentPage
    {
        private bool _isAltTheme;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnColumnHeaderTapped(object? sender, TappedEventArgs e)
        {
            if (BindingContext is MainViewModel vm)
            {
                string? colName = null;
                if (e.Parameter is string param && !string.IsNullOrEmpty(param))
                {
                    colName = param;
                }
                else if (sender is View view && view.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault()?.CommandParameter is string param2)
                {
                    colName = param2;
                }

                if (!string.IsNullOrEmpty(colName))
                {
                    bool isMultiSort = CheckIsMultiSortKeyPressed();
                    vm.ToggleSort(colName, isMultiSort);
                }
            }
        }

        private static bool CheckIsMultiSortKeyPressed()
        {
#if WINDOWS
            try
            {
                const int VK_SHIFT = 0x10;
                const int VK_CONTROL = 0x11;
                return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 || (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
#endif

        private void OnAddGroupPickerChanged(object? sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedItem is string selectedColumn)
            {
                if (BindingContext is MainViewModel vm)
                {
                    if (selectedColumn == "Nenhum")
                    {
                        vm.ActiveGroups.Clear();
                        vm.AddGroup(""); // refresh
                    }
                    else
                    {
                        string colName = selectedColumn switch
                        {
                            "Data" => "DataProcessamento",
                            "Estado" => "Estado",
                            "Tentativas" or "Retry" => "numRetry",
                            "Ficheiro" => "NomeFicheiro",
                            _ => selectedColumn
                        };
                        vm.AddGroup(colName);
                    }
                }
                picker.SelectedIndex = -1; // reset selection
            }
        }

        private void OnThemeToggleClicked(object? sender, EventArgs e)
        {
            _isAltTheme = !_isAltTheme;
            if (Application.Current?.Resources != null)
            {
                if (_isAltTheme)
                {
                    Application.Current.Resources["HeaderPurple"] = Color.FromArgb("#4A148C"); // Deep Indigo Purple
                    Application.Current.Resources["Primary"] = Color.FromArgb("#7B1FA2");
                }
                else
                {
                    Application.Current.Resources["HeaderPurple"] = Color.FromArgb("#653FA0"); // Vibrant Violet
                    Application.Current.Resources["Primary"] = Color.FromArgb("#6841CA");
                }
            }
        }
    }
}
