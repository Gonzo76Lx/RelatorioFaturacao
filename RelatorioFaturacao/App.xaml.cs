using Microsoft.Extensions.DependencyInjection;

namespace RelatorioFaturacao;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell())
        {
            Title = "Relatório de Faturação - Desenvolvido por Gonçalo Nuno Santos",
            MinimumWidth = 960,
            MinimumHeight = 640,
            Width = 1440,
            Height = 900
        };
        return window;
    }
}