using BrownianMotionSimulator.Model.Helpers;
using BrownianMotionSimulator.View.Widgets.Helpers;
using BrownianMotionSimulator.ViewModel.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrownianMotionSimulator.ViewModel
{
    public partial class HomeViewModel : BaseViewModel
    {
        [ObservableProperty] private double initialPrice = 100.0;
        [ObservableProperty] private double volatilityInput = 2.0;     // % se UsePercentInputs
        [ObservableProperty] private double meanReturnInput = 0.05;    // % se UsePercentInputs
        [ObservableProperty] private int durationDays = 252;
        [ObservableProperty] private bool usePercentInputs = true;
        [ObservableProperty] private bool parametersAreAnnualized = false;
        [ObservableProperty] private int simulations = 3;
        [ObservableProperty] private bool showGrid = true;
        [ObservableProperty] private bool showLegend = true;
        [ObservableProperty] private bool yAxisCurrency = false;
        [ObservableProperty] private double lineThickness = 2.0;

        public List<string> LineStyleOptions { get; } = new() { "Sólida", "Tracejada", "Pontilhada" };
        [ObservableProperty] private string selectedLineStyle = "Sólida";

        public List<string> PaletteOptions { get; } = new() { "Vibrante", "Pastel", "Monocromático Azul", "Monocromático Verde", "Arco-Íris" };
        [ObservableProperty] private string selectedPalette = "Vibrante";

        [ObservableProperty] private PriceSeriesDrawable chart = new();

        public event EventHandler? RedrawRequested;

        public HomeViewModel() { }

        [RelayCommand]
        private void Simulate()
        {
            if (DurationDays < 2) DurationDays = 2;
            if (InitialPrice <= 0) InitialPrice = 1;
            if (Simulations < 1) Simulations = 1;

            double mu = MeanReturnInput;
            double sig = VolatilityInput;

            if (UsePercentInputs)
            {
                mu /= 100.0;
                sig /= 100.0;
            }

            double meanStep, sigmaStep;

            if (ParametersAreAnnualized)
            {
                const double tradingDays = 252.0;
                double dt = 1.0 / tradingDays;

                meanStep = (mu - 0.5 * sig * sig) * dt;
                sigmaStep = sig * Math.Sqrt(dt);
            }
            else
            {
                meanStep = mu;
                sigmaStep = sig;
            }

      
            var seriesList = new List<IReadOnlyList<double>>(Simulations);
            for (int i = 0; i < Simulations; i++)
            {
                var series = Utility.GenerateBrownianMotion(
                    sigma: sigmaStep,
                    mean: meanStep,
                    initialPrice: InitialPrice,
                    numDays: DurationDays
                );
                seriesList.Add(series);
            }


            Chart.Series = seriesList;
            Chart.SeriesColors = BuildPalette(SelectedPalette, Simulations);
            Chart.StrokeSize = (float)LineThickness;
            Chart.ShowGrid = ShowGrid;
            Chart.ShowLegend = ShowLegend;
            Chart.UseCurrencyFormat = YAxisCurrency;
            Chart.LineStyle = SelectedLineStyle switch
            {
                "Tracejada" => LineStyleOption.Dashed,
                "Pontilhada" => LineStyleOption.Dotted,
                _ => LineStyleOption.Solid
            };

            RedrawRequested?.Invoke(this, EventArgs.Empty);
        }

        private static IList<Color> BuildPalette(string name, int n)
        {
            // Paletas simples e seguras (sem libs externas)
            List<Color> baseList = name switch
            {
                "Pastel" => new()
                {
                    Color.FromArgb("#8EC5FC"), Color.FromArgb("#FFC3A0"), Color.FromArgb("#B8E1FF"),
                    Color.FromArgb("#D4E157"), Color.FromArgb("#FFAB91"), Color.FromArgb("#F8BBD0"),
                    Color.FromArgb("#C5E1A5"), Color.FromArgb("#B39DDB"), Color.FromArgb("#FFE082"),
                    Color.FromArgb("#80DEEA")
                },
                "Monocromático Azul" => new()
                {
                    Color.FromArgb("#1e88e5"), Color.FromArgb("#1976d2"), Color.FromArgb("#1565c0"),
                    Color.FromArgb("#0d47a1"), Color.FromArgb("#42a5f5"), Color.FromArgb("#90caf9")
                },
                "Monocromático Verde" => new()
                {
                    Color.FromArgb("#2e7d32"), Color.FromArgb("#1b5e20"), Color.FromArgb("#43a047"),
                    Color.FromArgb("#66bb6a"), Color.FromArgb("#81c784"), Color.FromArgb("#a5d6a7")
                },
                "Arco-Íris" => new()
                {
                    Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Indigo, Colors.Violet
                },
                _ => new() // Vibrante (default)
                {
                    Colors.DodgerBlue, Colors.OrangeRed, Colors.MediumSeaGreen,
                    Colors.MediumOrchid, Colors.Goldenrod, Colors.CadetBlue,
                    Colors.Tomato, Colors.Teal, Colors.CornflowerBlue, Colors.IndianRed
                }
            };

            var colors = new List<Color>(n);
            for (int i = 0; i < n; i++)
                colors.Add(baseList[i % baseList.Count]);
            return colors;
        }
    }
}
