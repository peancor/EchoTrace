using System.Windows.Threading;
using EchoTrace.App.ViewModels;
using ScottPlot;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace EchoTrace.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _chartTimer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.ThemeChanged += (_, _) =>
        {
            ApplyAppTheme();
            RenderChart();
        };
        _viewModel.ChartChanged += (_, _) =>
        {
            if (!_viewModel.IsChartPaused)
            {
                RenderChart();
            }
        };

        _chartTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _chartTimer.Tick += (_, _) =>
        {
            if (!_viewModel.IsChartPaused)
            {
                RenderChart();
            }
        };
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += async (_, _) =>
        {
            _chartTimer.Stop();
            await _viewModel.ShutdownAsync();
        };
        _chartTimer.Start();
        ApplyAppTheme();
        RenderChart();
    }

    private void RenderChart()
    {
        RenderRssiChart();
        RenderEventRateChart();
    }

    private void RenderRssiChart()
    {
        RssiPlot.Plot.Clear();
        ApplyPlotTheme(RssiPlot.Plot);
        IReadOnlyList<RssiPoint> points = _viewModel.SelectedRssiPoints;
        if (points.Count > 0)
        {
            double[] xs = points.Select(point => point.Timestamp.ToOADate()).ToArray();
            double[] ys = points.Select(point => point.Value).ToArray();
            var raw = RssiPlot.Plot.Add.Scatter(xs, ys);
            raw.LegendText = "Raw RSSI";
            string rawColor = _viewModel.IsLightTheme ? "#0077B6" : "#55C7F7";
            raw.LineColor = Color.FromHex(rawColor);
            raw.MarkerFillColor = Color.FromHex(rawColor);
            raw.MarkerLineColor = Color.FromHex(rawColor);
            raw.LineWidth = 1.4f;
            raw.MarkerSize = 3;
            if (ys.Length > 2)
            {
                double[] smoothed = Smooth(ys, 0.28);
                var trend = RssiPlot.Plot.Add.Scatter(xs, smoothed);
                trend.LegendText = "Smoothed";
                trend.LineColor = Color.FromHex(_viewModel.IsLightTheme ? "#B45309" : "#F2C14E");
                trend.MarkerSize = 0;
                trend.LineWidth = 2.4f;
            }
            RssiPlot.Plot.Axes.DateTimeTicksBottom();
            ApplyRollingTimeAxis(RssiPlot.Plot, -100, -25);
        }
        else
        {
            ApplyRollingTimeAxis(RssiPlot.Plot, -100, -25);
        }

        RssiPlot.Plot.Title(_viewModel.SelectedChartTitle);
        RssiPlot.Plot.YLabel("RSSI dBm");
        RssiPlot.Plot.XLabel("Time");
        RssiPlot.Plot.Legend.IsVisible = points.Count > 2;
        ApplyPlotTextColors(RssiPlot.Plot);
        RssiPlot.Refresh();
    }

    private void RenderEventRateChart()
    {
        EventRatePlot.Plot.Clear();
        ApplyPlotTheme(EventRatePlot.Plot);
        IReadOnlyList<RssiPoint> points = _viewModel.EventRatePoints;
        if (points.Count > 0)
        {
            double[] xs = points.Select(point => point.Timestamp.ToOADate()).ToArray();
            double[] ys = points.Select(point => point.Value).ToArray();
            var rate = EventRatePlot.Plot.Add.Scatter(xs, ys);
            string rateColor = _viewModel.IsLightTheme ? "#047857" : "#4DB6AC";
            rate.LineColor = Color.FromHex(rateColor);
            rate.MarkerFillColor = Color.FromHex(rateColor);
            rate.MarkerLineColor = Color.FromHex(rateColor);
            rate.LineWidth = 1.8f;
            rate.MarkerSize = 3;
            EventRatePlot.Plot.Axes.DateTimeTicksBottom();
            double yMax = Math.Max(5, Math.Ceiling(ys.Max() + 2));
            ApplyRollingTimeAxis(EventRatePlot.Plot, 0, yMax);
        }
        else
        {
            ApplyRollingTimeAxis(EventRatePlot.Plot, 0, 5);
        }

        EventRatePlot.Plot.Title("Events / second");
        EventRatePlot.Plot.YLabel("events/s");
        EventRatePlot.Plot.XLabel("Time");
        ApplyPlotTextColors(EventRatePlot.Plot);
        EventRatePlot.Refresh();
    }

    private void ApplyRollingTimeAxis(Plot plot, double yMin, double yMax)
    {
        double xMax = DateTime.Now.ToOADate();
        double xMin = DateTime.Now.AddSeconds(-_viewModel.TimeWindowSeconds).ToOADate();
        plot.Axes.SetLimits(xMin, xMax, yMin, yMax);
    }

    private void ApplyPlotTheme(Plot plot)
    {
        bool light = _viewModel.IsLightTheme;
        plot.SetStyle(new PlotStyle
        {
            FigureBackgroundColor = Color.FromHex(light ? "#FFFFFF" : "#111A22"),
            DataBackgroundColor = Color.FromHex(light ? "#F8FAFC" : "#0D141C"),
            AxisColor = Color.FromHex(light ? "#334155" : "#9AAABB"),
            GridMajorLineColor = Color.FromHex(light ? "#CBD5E1" : "#253746"),
            LegendBackgroundColor = Color.FromHex(light ? "#FFFFFF" : "#111A22"),
            LegendFontColor = Color.FromHex(light ? "#0F172A" : "#E7EDF3"),
            LegendOutlineColor = Color.FromHex(light ? "#CBD5E1" : "#314554")
        });
        plot.Axes.Color(Color.FromHex(light ? "#334155" : "#9AAABB"));
        plot.Grid.MajorLineColor = Color.FromHex(light ? "#CBD5E1" : "#253746");
        Color labelColor = Color.FromHex(light ? "#0F172A" : "#C9D6E2");
        plot.Axes.Title.Label.ForeColor = labelColor;
        plot.Axes.Left.Label.ForeColor = labelColor;
        plot.Axes.Left.TickLabelStyle.ForeColor = labelColor;
        plot.Axes.Bottom.Label.ForeColor = labelColor;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = labelColor;
    }

    private void ApplyPlotTextColors(Plot plot)
    {
        Color labelColor = Color.FromHex(_viewModel.IsLightTheme ? "#0F172A" : "#DCE7F3");
        plot.Axes.Title.Label.ForeColor = labelColor;
        plot.Axes.Left.Label.ForeColor = labelColor;
        plot.Axes.Left.TickLabelStyle.ForeColor = labelColor;
        plot.Axes.Bottom.Label.ForeColor = labelColor;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = labelColor;
    }

    private void ApplyAppTheme()
    {
        bool light = _viewModel.IsLightTheme;
        ApplicationThemeManager.Apply(light ? ApplicationTheme.Light : ApplicationTheme.Dark);
        SetBrush("EchoAppBackgroundBrush", light ? "#EEF2F6" : "#0D1117");
        SetBrush("EchoPanelBrush", light ? "#FFFFFF" : "#111A22");
        SetBrush("EchoPanelAltBrush", light ? "#F8FAFC" : "#16212B");
        SetBrush("EchoBorderBrush", light ? "#CBD5E1" : "#263746");
        SetBrush("EchoTextBrush", light ? "#0F172A" : "#E7EDF3");
        SetBrush("EchoMutedTextBrush", light ? "#64748B" : "#92A3B3");
        SetBrush("EchoAccentBrush", light ? "#00796B" : "#4DB6AC");
        SetBrush("EchoInputBackgroundBrush", light ? "#FFFFFF" : "#0F171F");
        SetBrush("EchoComboDropDownBackgroundBrush", light ? "#FFFFFF" : "#F8FAFC");
        SetBrush("EchoComboDropDownHoverBrush", light ? "#E0F2FE" : "#DCEBFA");
        SetBrush("EchoComboDropDownSelectedBrush", light ? "#CFE7E4" : "#CFE7E4");
        SetBrush("EchoComboTextBrush", "#0F172A");
        SetBrush("EchoGridLineBrush", light ? "#E2E8F0" : "#253746");
        SetBrush("EchoListBackgroundBrush", light ? "#FFFFFF" : "#111A22");
        SetBrush("EchoActivityTextBrush", light ? "#334155" : "#D5DEE7");
    }

    private void SetBrush(string resourceKey, string hexColor)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
        Resources[resourceKey] = new System.Windows.Media.SolidColorBrush(color);
    }

    private static double[] Smooth(double[] values, double alpha)
    {
        double[] smoothed = new double[values.Length];
        smoothed[0] = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            smoothed[i] = alpha * values[i] + (1 - alpha) * smoothed[i - 1];
        }

        return smoothed;
    }
}
