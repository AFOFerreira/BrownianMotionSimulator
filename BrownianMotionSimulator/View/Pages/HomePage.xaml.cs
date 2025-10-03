using BrownianMotionSimulator.ViewModel;

namespace BrownianMotionSimulator.View.Pages;

public partial class HomePage : ContentPage
{
    private HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();

        BindingContext = _vm = vm;

        _vm.RedrawRequested += (_, __) => MainThread.BeginInvokeOnMainThread(() => ChartView?.Invalidate());

        ChartView.SizeChanged += (_, __) => ChartView?.Invalidate();
    }

    private async void OnPercentHelpClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Entradas em %", "Quando ativado, Volatilidade e Média de retorno são interpretadas como porcentagens (ex.: 2 = 2%).", "OK");
    }

    private async void OnAnnualHelpClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Parâmetros anualizados", "Quando ativado, convertemos os parâmetros anuais para passos diários (252 dias).", "OK");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ChartView?.Invalidate();
    }
}
