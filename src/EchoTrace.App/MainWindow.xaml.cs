using System.Windows;
using System.Windows.Threading;
using EchoTrace.App.ViewModels;
using ScottPlot;

namespace EchoTrace.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _chartTimer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.ChartChanged += (_, _) =>
        {
            if (!_viewModel.IsChartPaused)
            {
                RenderChart();
            }
        };
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += async (_, _) => await _viewModel.ShutdownAsync();
        _chartTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _chartTimer.Tick += (_, _) =>
        {
            if (!_viewModel.IsChartPaused)
            {
                RenderChart();
            }
        };
        _chartTimer.Start();
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
            raw.LineColor = Color.FromHex("#55C7F7");
            raw.MarkerFillColor = Color.FromHex("#55C7F7");
            raw.MarkerLineColor = Color.FromHex("#55C7F7");
            raw.LineWidth = 1.4f;
            raw.MarkerSize = 3;
            if (ys.Length > 2)
            {
                double[] smoothed = Smooth(ys, 0.28);
                var trend = RssiPlot.Plot.Add.Scatter(xs, smoothed);
                trend.LegendText = "Smoothed";
                trend.LineColor = Color.FromHex("#F2C14E");
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
            rate.LineColor = Color.FromHex("#4DB6AC");
            rate.MarkerFillColor = Color.FromHex("#4DB6AC");
            rate.MarkerLineColor = Color.FromHex("#4DB6AC");
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
        EventRatePlot.Refresh();
    }

    private void ApplyRollingTimeAxis(Plot plot, double yMin, double yMax)
    {
        double xMax = DateTime.Now.ToOADate();
        double xMin = DateTime.Now.AddSeconds(-_viewModel.TimeWindowSeconds).ToOADate();
        plot.Axes.SetLimits(xMin, xMax, yMin, yMax);
    }

    private static void ApplyPlotTheme(Plot plot)
    {
        plot.SetStyle(new PlotStyle
        {
            FigureBackgroundColor = Color.FromHex("#111A22"),
            DataBackgroundColor = Color.FromHex("#0D141C"),
            AxisColor = Color.FromHex("#9AAABB"),
            GridMajorLineColor = Color.FromHex("#253746"),
            LegendBackgroundColor = Color.FromHex("#111A22"),
            LegendFontColor = Color.FromHex("#E7EDF3"),
            LegendOutlineColor = Color.FromHex("#314554")
        });
        plot.Axes.Color(Color.FromHex("#9AAABB"));
        plot.Grid.MajorLineColor = Color.FromHex("#253746");
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
