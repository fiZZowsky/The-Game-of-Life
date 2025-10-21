using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ConwayGameOfLifeWPF
{
    public partial class MainWindow : Window
    {
        private const int GridWidth = 400;
        private const int GridHeight = 300;
        private const int PixelSize = 2;

        private GameOfLifeEngine _engine;
        private WriteableBitmap _bitmap;
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private readonly byte[] _aliveColor = { 255, 255, 255, 255 };
        private readonly byte[] _deadColor = { 0, 0, 0, 255 };
        private byte[] _pixelData;
        private int _stride;

        private readonly Dictionary<string, List<ConwayGameOfLifeWPF.Point>> _patternLibrary = new Dictionary<string, List<ConwayGameOfLifeWPF.Point>>();

        private bool _isLoggingAnalysis = false;
        private StringBuilder _csvBuilder;

        private Dictionary<string, int> _generationHistory = new Dictionary<string, int>();
        private bool _cycleDetected = false;

        public MainWindow()
        {
            InitializeComponent();
            PopulatePatternLibrary();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeSimulation();
            _timer.Interval = TimeSpan.FromMilliseconds(SpeedSlider.Value);
            _timer.Tick += GameTimer_Tick;

            foreach (var patternName in _patternLibrary.Keys)
            {
                PatternComboBox.Items.Add(patternName);
            }
            PatternComboBox.SelectedIndex = 0;
        }

        private void PopulatePatternLibrary()
        {
            _patternLibrary.Add("Szybowiec (Glider)", new List<ConwayGameOfLifeWPF.Point>
            { new ConwayGameOfLifeWPF.Point(1, 0), new ConwayGameOfLifeWPF.Point(2, 1), new ConwayGameOfLifeWPF.Point(0, 2), new ConwayGameOfLifeWPF.Point(1, 2), new ConwayGameOfLifeWPF.Point(2, 2) });
            _patternLibrary.Add("R-pentomino", new List<ConwayGameOfLifeWPF.Point>
            { new ConwayGameOfLifeWPF.Point(1, 0), new ConwayGameOfLifeWPF.Point(2, 0), new ConwayGameOfLifeWPF.Point(0, 1), new ConwayGameOfLifeWPF.Point(1, 1), new ConwayGameOfLifeWPF.Point(1, 2) });
            _patternLibrary.Add("Działo Szybowcowe (Glider Gun)", new List<ConwayGameOfLifeWPF.Point>
            { new ConwayGameOfLifeWPF.Point(24, 0), new ConwayGameOfLifeWPF.Point(22, 1), new ConwayGameOfLifeWPF.Point(24, 1), new ConwayGameOfLifeWPF.Point(12, 2), new ConwayGameOfLifeWPF.Point(13, 2), new ConwayGameOfLifeWPF.Point(20, 2), new ConwayGameOfLifeWPF.Point(21, 2), new ConwayGameOfLifeWPF.Point(34, 2), new ConwayGameOfLifeWPF.Point(35, 2), new ConwayGameOfLifeWPF.Point(11, 3), new ConwayGameOfLifeWPF.Point(15, 3), new ConwayGameOfLifeWPF.Point(20, 3), new ConwayGameOfLifeWPF.Point(21, 3), new ConwayGameOfLifeWPF.Point(34, 3), new ConwayGameOfLifeWPF.Point(35, 3), new ConwayGameOfLifeWPF.Point(0, 4), new ConwayGameOfLifeWPF.Point(1, 4), new ConwayGameOfLifeWPF.Point(10, 4), new ConwayGameOfLifeWPF.Point(16, 4), new ConwayGameOfLifeWPF.Point(20, 4), new ConwayGameOfLifeWPF.Point(21, 4), new ConwayGameOfLifeWPF.Point(0, 5), new ConwayGameOfLifeWPF.Point(1, 5), new ConwayGameOfLifeWPF.Point(10, 5), new ConwayGameOfLifeWPF.Point(14, 5), new ConwayGameOfLifeWPF.Point(16, 5), new ConwayGameOfLifeWPF.Point(17, 5), new ConwayGameOfLifeWPF.Point(22, 5), new ConwayGameOfLifeWPF.Point(24, 5), new ConwayGameOfLifeWPF.Point(10, 6), new ConwayGameOfLifeWPF.Point(16, 6), new ConwayGameOfLifeWPF.Point(24, 6), new ConwayGameOfLifeWPF.Point(11, 7), new ConwayGameOfLifeWPF.Point(15, 7), new ConwayGameOfLifeWPF.Point(12, 8), new ConwayGameOfLifeWPF.Point(13, 8) });
        }

        private void InitializeSimulation(bool random = true)
        {
            _engine = new GameOfLifeEngine(GridWidth, GridHeight);
            if (random)
            {
                _engine.Randomize(0.5);
            }

            int bitmapWidth = GridWidth * PixelSize;
            int bitmapHeight = GridHeight * PixelSize;
            _bitmap = new WriteableBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Bgra32, null);
            GameImage.Source = _bitmap;
            _stride = bitmapWidth * (_bitmap.Format.BitsPerPixel / 8);
            _pixelData = new byte[bitmapHeight * _stride];

            ResetAnalysisState();
            RenderGrid();
            UpdateAnalyticsUI();
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (_cycleDetected)
            {
                _timer.Stop();
                return;
            }

            _engine.Update();
            RenderGrid();
            UpdateAnalyticsUI();

            if (_isLoggingAnalysis)
            {
                LogCurrentFrameData();
            }

            try
            {
                string currentHash = _engine.GetBinaryGridHash();
                if (_generationHistory.ContainsKey(currentHash))
                {
                    int previousGeneration = _generationHistory[currentHash];
                    int period = _engine.GenerationCount - previousGeneration;

                    _timer.Stop();
                    _cycleDetected = true;

                    string message = (period == 1) ?
                        $"Wykryto stabilizację (stan stały)." :
                        $"Wykryto cykl o okresie {period}.";

                    CycleStatusText.Text = message;
                    MessageBox.Show($"{message}\n(Powtórzenie stanu z pokolenia {previousGeneration})", "Wykryto Cykl");
                }
                else
                {
                    if (_generationHistory.Count > 2000) _generationHistory.Clear();
                    _generationHistory.Add(currentHash, _engine.GenerationCount);
                }
            }
            catch (Exception ex)
            {
                _timer.Stop();
                MessageBox.Show($"Błąd podczas haszowania: {ex.Message}");
            }
        }

        private void RenderGrid()
        {
            bool[,] grid = _engine.GetCurrentGrid();
            _bitmap.Lock();
            try
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        byte[] color = grid[x, y] ? _aliveColor : _deadColor;
                        int startX = x * PixelSize;
                        int startY = y * PixelSize;

                        for (int py = 0; py < PixelSize; py++)
                        {
                            int lineIndex = (startY + py) * _stride;
                            for (int px = 0; px < PixelSize; px++)
                            {
                                int index = lineIndex + (startX + px) * 4;
                                _pixelData[index + 0] = color[0]; // B
                                _pixelData[index + 1] = color[1]; // G
                                _pixelData[index + 2] = color[2]; // R
                                _pixelData[index + 3] = color[3]; // A
                            }
                        }
                    }
                }
                _bitmap.WritePixels(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight), _pixelData, _stride, 0);
            }
            finally
            {
                _bitmap.Unlock();
            }
        }

        private void UpdateAnalyticsUI()
        {
            GenerationText.Text = $"Pokolenie: {_engine.GenerationCount}";
            PopulationText.Text = $"Populacja: {_engine.PopulationCount}";

            if (_engine.GenerationCount % 10 == 0)
            {
                double entropy = _engine.CalculateShannonEntropy(2);
                EntropyText.Text = $"Entropia (2x2): {entropy:F4}";

                double dimension = CalculateFractalDimension();
                FractalDimensionText.Text = $"Wymiar (D): {dimension:F4}";
            }
        }

        private double CalculateFractalDimension()
        {
            var points = new List<Tuple<double, double>>();
            int[] boxSizes = { 2, 4, 8, 16, 32 };

            foreach (int s in boxSizes)
            {
                int Ns = _engine.CountActiveBoxes(s);
                if (Ns > 0)
                {
                    points.Add(new Tuple<double, double>(Math.Log(1.0 / s), Math.Log(Ns)));
                }
            }

            if (points.Count < 2) return 0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = points.Count;

            foreach (var p in points)
            {
                sumX += p.Item1;
                sumY += p.Item2;
                sumXY += p.Item1 * p.Item2;
                sumX2 += p.Item1 * p.Item1;
            }

            double denominator = (n * sumX2 - sumX * sumX);
            if (Math.Abs(denominator) < 1e-9) return 0;

            double slope = (n * sumXY - sumX * sumY) / denominator;
            return Math.Abs(slope);
        }

        private void ResetAnalysisState()
        {
            _generationHistory.Clear();
            _cycleDetected = false;
            if (CycleStatusText != null)
                CycleStatusText.Text = "";
        }

        private void StartButton_Click(object sender, RoutedEventArgs e) { _timer.Start(); }
        private void StopButton_Click(object sender, RoutedEventArgs e) { _timer.Stop(); }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            GameTimer_Tick(null, null);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            InitializeSimulation(random: true);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _engine.Clear();
            ResetAnalysisState();
            RenderGrid();
            UpdateAnalyticsUI();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(e.NewValue);
        }

        private void LoadPatternButton_Click(object sender, RoutedEventArgs e)
        {
            if (PatternComboBox.SelectedItem == null) return;
            string patternName = PatternComboBox.SelectedItem.ToString();
            if (_patternLibrary.ContainsKey(patternName))
            {
                _timer.Stop();
                _engine.LoadPattern(_patternLibrary[patternName], GridWidth / 3, GridHeight / 3);
                ResetAnalysisState();
                RenderGrid();
                UpdateAnalyticsUI();
            }
        }

        private void GameImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { SetCellFromMouse(e); }
        private void GameImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) SetCellFromMouse(e);
        }

        private void SetCellFromMouse(MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(GameImage);
            double actualWidth = GameImage.ActualWidth;
            double actualHeight = GameImage.ActualHeight;
            double scaleX = _bitmap.PixelWidth / actualWidth;
            double scaleY = _bitmap.PixelHeight / actualHeight;
            int gridX = (int)((p.X * scaleX) / PixelSize);
            int gridY = (int)((p.Y * scaleY) / PixelSize);

            _engine.SetCell(gridX, gridY, true);
            RenderGrid();
            UpdateAnalyticsUI();
            ResetAnalysisState();
        }

        private void StartAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingAnalysis = true;
            _csvBuilder = new StringBuilder();
            _csvBuilder.AppendLine("Generacja;Populacja;Entropia_2x2;Wymiar_D;BoxCount_s2;BoxCount_s4;BoxCount_s8;BoxCount_s16;BoxCount_s32");

            StartAnalysisButton.IsEnabled = false;
            StopAnalysisButton.IsEnabled = true;
            _timer.Start();
        }

        private void LogCurrentFrameData()
        {
            int gen = _engine.GenerationCount;
            int pop = _engine.PopulationCount;
            double entropy = _engine.CalculateShannonEntropy(2);
            double dimension = CalculateFractalDimension();
            int n2 = _engine.CountActiveBoxes(2);
            int n4 = _engine.CountActiveBoxes(4);
            int n8 = _engine.CountActiveBoxes(8);
            int n16 = _engine.CountActiveBoxes(16);
            int n32 = _engine.CountActiveBoxes(32);

            _csvBuilder.AppendLine($"{gen};{pop};{entropy:F4};{dimension:F4};{n2};{n4};{n8};{n16};{n32}");
        }

        private void StopAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingAnalysis = false;
            _timer.Stop();
            StartAnalysisButton.IsEnabled = true;
            StopAnalysisButton.IsEnabled = false;

            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "conway_analysis_bw.csv");
                File.WriteAllText(filePath, _csvBuilder.ToString());
                MessageBox.Show($"Analiza zapisana na pulpicie:\n{filePath}", "Zapisano Plik", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisu pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
        }
    }
}