using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System;

namespace EMAExtractor.UI
{
    public partial class ProgressWindow : Window
    {
        private readonly DateTime _startedAt = DateTime.Now;
        private readonly TextBlock _statusText;
        private readonly TextBlock _contextText;
        private readonly ProgressBar _mainProgressBar;
        private readonly TextBlock _percentText;
        private readonly TextBlock _metadataText;
        private readonly Button _cancelButton;

        public bool CancelRequested { get; private set; }

        public ProgressWindow()
            : this("", "", "")
        {
        }

        public ProgressWindow(string discipline, string scope, string outputPath)
            : this("EMA Export Progress", discipline, scope, outputPath)
        {
        }

        public ProgressWindow(string title, string discipline, string scope, string outputPath)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "EMA Export Progress" : title;
            Height = 230;
            Width = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;

            _contextText = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap,
                Text = $"Discipline: {discipline} | Scope: {scope}\nOutput: {outputPath}"
            };

            _statusText = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                Text = "Starting...",
                TextWrapping = TextWrapping.Wrap
            };

            _mainProgressBar = new ProgressBar
            {
                Height = 22,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            _percentText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                Text = "0%"
            };

            _metadataText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Text = "Metadata: pending"
            };

            _cancelButton = new Button
            {
                Content = "Cancel after current item",
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _cancelButton.Click += (sender, args) =>
            {
                CancelRequested = true;
                _cancelButton.IsEnabled = false;
                _cancelButton.Content = "Cancelling...";
            };

            Grid layout = new Grid
            {
                Margin = new Thickness(12)
            };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(_contextText, 0);
            Grid.SetRow(_statusText, 1);
            Grid.SetRow(_mainProgressBar, 2);
            Grid.SetRow(_percentText, 3);
            Grid.SetRow(_metadataText, 4);
            Grid.SetRow(_cancelButton, 5);

            layout.Children.Add(_contextText);
            layout.Children.Add(_statusText);
            layout.Children.Add(_mainProgressBar);
            layout.Children.Add(_percentText);
            layout.Children.Add(_metadataText);
            layout.Children.Add(_cancelButton);

            Content = layout;
        }

        public void SetStatus(string status)
        {
            TimeSpan elapsed = DateTime.Now - _startedAt;
            _statusText.Text = $"Phase: {status}\nElapsed: {elapsed:mm\\:ss}";
            DoEvents();
        }

        public void SetProgress(int current, int total)
        {
            if (total <= 0)
            {
                total = 1;
            }

            double percent = (double)current / total * 100.0;
            _mainProgressBar.Value = percent;
            _percentText.Text = string.Format("{0:0}%", percent);
            DoEvents();
        }

        public void SetMetadataPath(string metadataPath)
        {
            _metadataText.Text = "Metadata: " + metadataPath;
            DoEvents();
        }

        private void DoEvents()
        {
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}
