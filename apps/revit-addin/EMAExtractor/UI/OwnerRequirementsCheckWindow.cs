using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using EMAExtractor.Requirements;

namespace EMAExtractor.UI
{
    public class OwnerRequirementsCheckWindow : Window
    {
        private readonly TextBox _requirementsPath;
        private readonly ComboBox _discipline;
        private readonly ComboBox _scope;
        private readonly TextBox _outputFolder;

        public string OwnerRequirementsFilePath => _requirementsPath.Text.Trim();
        public RequirementDiscipline SelectedDiscipline => ParseDiscipline(_discipline.Text);
        public RequirementModelScope SelectedScope => _scope.Text == "Current View"
            ? RequirementModelScope.CurrentView
            : RequirementModelScope.EntireModel;
        public string OutputFolder => _outputFolder.Text.Trim();

        public OwnerRequirementsCheckWindow(string modelName, string defaultOutputFolder)
        {
            Title = "EMA AI Load Owner Requirements";
            Width = 760;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            FontFamily = new FontFamily("Segoe UI");

            StackPanel root = new StackPanel
            {
                Margin = new Thickness(18)
            };

            root.Children.Add(new TextBlock
            {
                Text = "Load Owner Requirements",
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Select an Owner Requirements workbook, choose the discipline scope, and save the selection so the compliance check can run from the main EMA AI panel.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 18)
            });

            root.Children.Add(BuildReadOnlyRow("Model", string.IsNullOrWhiteSpace(modelName) ? "(active model)" : modelName));

            _requirementsPath = BuildBrowseRow(
                root,
                "Owner Requirements Excel",
                "Browse...",
                BrowseForWorkbook);

            _discipline = BuildComboRow(root, "Discipline", new[]
            {
                "All",
                "Electrical",
                "Lighting",
                "Mechanical",
                "Plumbing",
                "Technology"
            }, "Electrical");

            _scope = BuildComboRow(root, "Scope", new[]
            {
                "Entire Model",
                "Current View"
            }, "Entire Model");

            _outputFolder = BuildBrowseRow(
                root,
                "Report Output Folder",
                "Browse...",
                BrowseForFolder,
                string.IsNullOrWhiteSpace(defaultOutputFolder) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : defaultOutputFolder);

            root.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 10, 0, 14),
                Child = new TextBlock
                {
                    Text = "This step only loads the workbook selection for the deterministic compliance check. It does not approve official compliance, and it does not require the dashboard to be open.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
                }
            });

            root.Children.Add(BuildButtonRow());

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = root
            };
        }

        private StackPanel BuildButtonRow()
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button openFolder = CreateButton("Open Output Folder", (sender, args) => OpenOutputFolder());
            Button load = CreateButton("Load Requirements", (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(_requirementsPath.Text))
                {
                    MessageBox.Show(this, "Please choose an Owner Requirements workbook first.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_outputFolder.Text))
                {
                    MessageBox.Show(this, "Please choose a report output folder first.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            });

            Button cancel = CreateButton("Cancel", (sender, args) =>
            {
                DialogResult = false;
                Close();
            });

            row.Children.Add(openFolder);
            row.Children.Add(load);
            row.Children.Add(cancel);
            return row;
        }

        private TextBox BuildBrowseRow(StackPanel root, string label, string buttonText, RoutedEventHandler browseHandler, string initialValue = "")
        {
            Grid grid = CreateLabeledRow(root, label);
            TextBox textBox = new TextBox
            {
                Text = initialValue,
                MinHeight = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };

            Button browse = CreateButton(buttonText, browseHandler);

            grid.Children.Add(textBox);
            Grid.SetColumn(textBox, 0);
            grid.Children.Add(browse);
            Grid.SetColumn(browse, 1);

            return textBox;
        }

        private ComboBox BuildComboRow(StackPanel root, string label, string[] options, string defaultValue)
        {
            Grid grid = CreateLabeledRow(root, label);
            ComboBox combo = new ComboBox
            {
                MinHeight = 28,
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };

            foreach (string option in options)
            {
                combo.Items.Add(option);
            }

            combo.SelectedItem = options.FirstOrDefault(option => string.Equals(option, defaultValue, StringComparison.OrdinalIgnoreCase)) ?? options[0];

            grid.Children.Add(combo);
            Grid.SetColumn(combo, 0);
            Grid.SetColumnSpan(combo, 2);

            return combo;
        }

        private static TextBlock BuildReadOnlyRow(string label, string value)
        {
            return new TextBlock
            {
                Text = label + ": " + value,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private Grid CreateLabeledRow(Panel root, string label)
        {
            Grid container = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            container.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 10, 0)
            });

            root.Children.Add(container);
            return container;
        }

        private Button CreateButton(string label, RoutedEventHandler clickHandler)
        {
            Button button = new Button
            {
                Content = label,
                MinWidth = 132,
                MinHeight = 30,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(12, 4, 12, 4),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246))
            };

            button.Click += clickHandler;
            return button;
        }

        private void BrowseForWorkbook(object sender, RoutedEventArgs e)
        {
            WinForms.OpenFileDialog dialog = new WinForms.OpenFileDialog
            {
                Title = "Select Owner Requirements Workbook",
                Filter = "Excel Workbooks (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _requirementsPath.Text = dialog.FileName;
            }
        }

        private void BrowseForFolder(object sender, RoutedEventArgs e)
        {
            using (WinForms.FolderBrowserDialog dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder where the HTML report should be written.";
                dialog.SelectedPath = string.IsNullOrWhiteSpace(_outputFolder.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    : _outputFolder.Text;

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    _outputFolder.Text = dialog.SelectedPath;
                }
            }
        }

        private void OpenOutputFolder()
        {
            string folder = _outputFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(this, "Choose an output folder first.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static RequirementDiscipline ParseDiscipline(string value)
        {
            return RequirementDisciplineNormalizer.Parse(value, RequirementDiscipline.All);
        }
    }
}
