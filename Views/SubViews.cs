using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rere.Managers;
using Rere.Models;

namespace Rere.Views
{
    // ─── Activity View ───────────────────────────────────────────────────────────
    public class ActivityView : UserControl
    {
        public ActivityView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = MakeHeader("📋  سجل النشاط");
            Grid.SetRow(header, 0);

            var dg = new DataGrid
            {
                Background          = Brushes.Transparent,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush         = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness     = new Thickness(1),
                RowBackground       = Brushes.Transparent,
                AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                VerticalGridLinesBrush   = Brushes.Transparent,
                IsReadOnly          = true,
                AutoGenerateColumns = false,
                RowHeight           = 32,
                ItemsSource         = AppState.Shared.Logs
            };

            dg.Columns.Add(new DataGridTextColumn { Header = "النوع", Binding = new System.Windows.Data.Binding("Type"), Width = new DataGridLength(60) });
            dg.Columns.Add(new DataGridTextColumn { Header = "IP",    Binding = new System.Windows.Data.Binding("Ip"),   Width = new DataGridLength(130) });
            dg.Columns.Add(new DataGridTextColumn { Header = "الاسم", Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(140) });
            dg.Columns.Add(new DataGridTextColumn { Header = "الدولة",Binding = new System.Windows.Data.Binding("Country"), Width = new DataGridLength(60) });
            dg.Columns.Add(new DataGridTextColumn { Header = "المدينة",Binding = new System.Windows.Data.Binding("City"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dg.Columns.Add(new DataGridTextColumn { Header = "المدة", Binding = new System.Windows.Data.Binding("Stay"), Width = new DataGridLength(80) });
            dg.Columns.Add(new DataGridTextColumn { Header = "الوقت", Binding = new System.Windows.Data.Binding("Time"), Width = new DataGridLength(70) });

            Grid.SetRow(dg, 1);

            grid.Children.Add(header);
            grid.Children.Add(dg);
            Content = grid;
        }
    }

    // ─── History View ────────────────────────────────────────────────────────────
    public class HistoryView : UserControl
    {
        public HistoryView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = "🕐  التاريخ", FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center };
            var clearBtn = MakeButton("مسح التاريخ", "#EF4444");
            clearBtn.Margin = new Thickness(16, 0, 0, 0);
            clearBtn.Click += (_, _) => {
                ConfigManager.Shared.ClearHistory();
                AppState.Shared.LoadHistory();
                AppState.Shared.ShowToast("✓ تم مسح التاريخ", ToastType.Success);
            };

            panel.Children.Add(title);
            panel.Children.Add(clearBtn);

            var headerBorder = MakePanelHeader(panel);
            Grid.SetRow(headerBorder, 0);

            var dg = MakeDataGrid();
            dg.ItemsSource = AppState.Shared.History;
            dg.Columns.Add(MakeCol("IP",      "Ip",      130));
            dg.Columns.Add(MakeCol("الاسم",   "Name",    140));
            dg.Columns.Add(MakeCol("الدولة",  "Country", 60));
            dg.Columns.Add(MakeCol("المدينة", "City",    120));
            dg.Columns.Add(MakeCol("ISP",     "Isp",     1, star: true));
            dg.Columns.Add(MakeCol("المدة",   "Stay",    80));
            dg.Columns.Add(MakeCol("الوقت",   "Time",    70));

            Grid.SetRow(dg, 1);
            grid.Children.Add(headerBorder);
            grid.Children.Add(dg);
            Content = grid;
        }
    }

    // ─── Players View ────────────────────────────────────────────────────────────
    public class PlayersView : UserControl
    {
        private readonly DataGrid _dg;
        private System.Windows.Controls.TextBox _searchBox = new();

        public PlayersView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header
            var header = MakeHeader("👥  قاعدة اللاعبين");
            Grid.SetRow(header, 0);

            // Search bar
            _searchBox = new System.Windows.Controls.TextBox
            {
                Height = 34, Margin = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12
            };
            _searchBox.TextChanged += (_, _) => RefreshPlayers(_searchBox.Text);
            Grid.SetRow(_searchBox, 1);

            // Grid
            _dg = MakeDataGrid();
            _dg.Columns.Add(MakeCol("IP",      "Ip",      120));
            _dg.Columns.Add(MakeCol("الاسم",   "Name",    140));
            _dg.Columns.Add(MakeCol("المدينة", "City",    100));
            _dg.Columns.Add(MakeCol("ISP",     "Isp",     1, star: true));
            _dg.Columns.Add(MakeCol("Port",    "Port",    60));
            _dg.Columns.Add(MakeCol("موثوق",   "Trusted", 55));
            Grid.SetRow(_dg, 2);

            RefreshPlayers("");

            grid.Children.Add(header);
            grid.Children.Add(_searchBox);
            grid.Children.Add(_dg);
            Content = grid;
        }

        private void RefreshPlayers(string query)
        {
            var players = string.IsNullOrEmpty(query)
                ? LocalDatabaseManager.Shared.GetAllPlayers()
                : LocalDatabaseManager.Shared.SearchPlayers(query);
            _dg.ItemsSource = players;
        }
    }

    // ─── Filters View ────────────────────────────────────────────────────────────
    public class FiltersView : UserControl
    {
        public FiltersView()
        {
            var main = new Grid();
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            main.RowDefinitions.Add(new RowDefinition());

            Grid.SetRow(MakeHeader("🚫  الفلاتر"), 0);
            main.Children.Add(MakeHeader("🚫  الفلاتر"));

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0,52,0,0) };
            var sp = new StackPanel { Margin = new Thickness(20) };

            sp.Children.Add(MakeFilterSection("🏙️  المدن المحظورة",
                LocalDatabaseManager.Shared.GetAllBlockedCities(),
                city => { LocalDatabaseManager.Shared.AddBlockedCity(city); },
                city => { LocalDatabaseManager.Shared.RemoveBlockedCity(city); }));

            sp.Children.Add(MakeFilterSection("📡  ISP المحظورة",
                LocalDatabaseManager.Shared.GetAllBlockedISPs(),
                isp => { LocalDatabaseManager.Shared.AddBlockedISP(isp); },
                isp => { LocalDatabaseManager.Shared.RemoveBlockedISP(isp); }));

            sp.Children.Add(MakeFilterSection("🌍  الدول المحظورة",
                LocalDatabaseManager.Shared.GetAllBlockedCountries(),
                c => { LocalDatabaseManager.Shared.AddBlockedCountry(c); },
                c => { LocalDatabaseManager.Shared.RemoveBlockedCountry(c); }));

            scroll.Content = sp;
            main.Children.Add(scroll);
            Content = main;
        }

        private UIElement MakeFilterSection(string title, List<string> items,
            System.Action<string> onAdd, System.Action<string> onRemove)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(16)
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 0, 0, 10)
            });

            var addGrid = new Grid(); addGrid.ColumnDefinitions.Add(new ColumnDefinition());
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var input = new System.Windows.Controls.TextBox
            {
                Height = 32, Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0F)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(1), Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12
            };
            var addBtn = MakeButton("+ إضافة", "#3B82F6");
            addBtn.Margin = new Thickness(8, 0, 0, 0);
            addBtn.Click += (_, _) => {
                var val = input.Text.Trim();
                if (!string.IsNullOrEmpty(val)) { onAdd(val); input.Clear(); }
            };

            Grid.SetColumn(input, 0); Grid.SetColumn(addBtn, 1);
            addGrid.Children.Add(input); addGrid.Children.Add(addBtn);
            sp.Children.Add(addGrid);

            var chipPanel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var item in items)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0xEF, 0x44, 0x44)),
                    CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 6)
                };
                var chipGrid = new Grid();
                chipGrid.ColumnDefinitions.Add(new ColumnDefinition());
                chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                chipGrid.Children.Add(new TextBlock
                {
                    Text = item, Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                    FontFamily = new FontFamily("Segoe UI"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center
                });
                var removeBtn = new Button
                {
                    Content = "✕", Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    FontSize = 10, Margin = new Thickness(6, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = item
                };
                var capturedItem = item;
                removeBtn.Click += (_, _) => onRemove(capturedItem);
                Grid.SetColumn(removeBtn, 1);
                chipGrid.Children.Add(removeBtn);
                chip.Child = chipGrid;
                chipPanel.Children.Add(chip);
            }
            sp.Children.Add(chipPanel);
            border.Child = sp;
            return border;
        }
    }

    // ─── Settings View ───────────────────────────────────────────────────────────
    public class SettingsView : UserControl
    {
        private readonly AppSettings _settings;
        private readonly Dictionary<string, System.Windows.Controls.TextBox> _fields = new();

        public SettingsView()
        {
            _settings = ConfigManager.Shared.LoadSettings();

            var main = new Grid();
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            main.RowDefinitions.Add(new RowDefinition());

            var header = MakeHeader("⚙️  الإعدادات");
            Grid.SetRow(header, 0);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 52, 0, 0)
            };

            var sp = new StackPanel { Margin = new Thickness(20) };

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(20)
            };
            var cardSp = new StackPanel();

            cardSp.Children.Add(MakeSettingRow("تأخير الطابور (ثانية)", "QueueDelay", _settings.QueueDelay.ToString()));
            cardSp.Children.Add(MakeSettingRow("مدة rere (ثانية)", "RereDuration", _settings.RereDuration.ToString()));
            cardSp.Children.Add(MakeSettingRow("تأخير إعادة المحاولة", "RetryDelay", _settings.RetryDelay.ToString()));
            cardSp.Children.Add(MakeSettingRow("نافذة تأهيل الخروج (ثانية)", "ExitAlert", _settings.ExitAlert.ToString()));
            cardSp.Children.Add(MakeSettingRow("وقت انتهاء الجلسة (ثانية)", "ExitTimeout", _settings.ExitTimeout.ToString()));

            var saveBtn = MakeButton("💾  حفظ الإعدادات", "#22C55E");
            saveBtn.Margin = new Thickness(0, 16, 0, 0);
            saveBtn.Click += (_, _) => SaveSettings();
            cardSp.Children.Add(saveBtn);

            card.Child = cardSp;
            sp.Children.Add(card);
            scroll.Content = sp;

            main.Children.Add(header);
            main.Children.Add(scroll);
            Content = main;
        }

        private UIElement MakeSettingRow(string label, string key, string value)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            row.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            var input = new System.Windows.Controls.TextBox
            {
                Text = value, Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0F)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(1), Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12
            };
            _fields[key] = input;

            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private void SaveSettings()
        {
            if (int.TryParse(_fields["QueueDelay"].Text, out var qd))  _settings.QueueDelay  = qd;
            if (int.TryParse(_fields["RereDuration"].Text, out var rd)) _settings.RereDuration = rd;
            if (int.TryParse(_fields["RetryDelay"].Text, out var ret))  _settings.RetryDelay  = ret;
            if (int.TryParse(_fields["ExitAlert"].Text, out var ea))    _settings.ExitAlert   = ea;
            if (int.TryParse(_fields["ExitTimeout"].Text, out var et))  _settings.ExitTimeout = et;

            ConfigManager.Shared.SaveSettings(_settings);
            AppState.Shared.ShowToast("✅ تم حفظ الإعدادات", ToastType.Success);
        }
    }

    // ─── Shared helpers ──────────────────────────────────────────────────────────
    file static class ViewHelpers
    {
        public static Border MakeHeader(string title)
        {
            var border = new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                BorderBrush   = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height        = 52
            };
            border.Child = new TextBlock
            {
                Text       = title,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin     = new Thickness(16, 0, 0, 0)
            };
            return border;
        }

        public static Border MakePanelHeader(UIElement child)
        {
            var border = new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                BorderBrush   = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height        = 52, Padding = new Thickness(16, 0, 16, 0)
            };
            border.Child = child;
            return border;
        }

        public static DataGrid MakeDataGrid()
            => new DataGrid
            {
                Background          = Brushes.Transparent,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush         = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness     = new Thickness(1),
                RowBackground       = Brushes.Transparent,
                AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                VerticalGridLinesBrush   = Brushes.Transparent,
                IsReadOnly          = true,
                AutoGenerateColumns = false,
                RowHeight           = 32,
                FontFamily          = new FontFamily("Segoe UI"),
                FontSize            = 12
            };

        public static DataGridTextColumn MakeCol(string header, string binding, double width, bool star = false)
            => new DataGridTextColumn
            {
                Header  = header,
                Binding = new System.Windows.Data.Binding(binding),
                Width   = star
                    ? new DataGridLength(width, DataGridLengthUnitType.Star)
                    : new DataGridLength(width)
            };

        public static Button MakeButton(string text, string hexColor)
        {
            var btn = new Button
            {
                Content         = text,
                Height          = 32,
                Padding         = new Thickness(14, 0, 14, 0),
                Background      = (SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hexColor)!,
                Foreground      = Brushes.White,
                BorderBrush     = Brushes.Transparent,
                FontFamily      = new FontFamily("Segoe UI"),
                FontSize        = 12,
                FontWeight      = FontWeights.SemiBold,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            btn.Template = (ControlTemplate)System.Windows.Application.Current.Resources.Contains("PrimaryBtn")
                ? null!
                : CreateRoundedTemplate(hexColor);
            return btn;
        }

        private static ControlTemplate CreateRoundedTemplate(string hexColor)
        {
            var template = new ControlTemplate(typeof(Button));
            var factory  = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty,
                (SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hexColor)!);
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            factory.SetValue(Border.PaddingProperty, new Thickness(14, 0, 14, 0));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(content);
            template.VisualTree = factory;
            return template;
        }
    }

    // Make helpers available inside this file as extension / file-scoped
    partial class ActivityView { static Border MakeHeader(string t) => ViewHelpers.MakeHeader(t); }
    partial class HistoryView  { static Border MakeHeader(string t) => ViewHelpers.MakeHeader(t);
                                  static Border MakePanelHeader(UIElement e) => ViewHelpers.MakePanelHeader(e);
                                  static DataGrid MakeDataGrid() => ViewHelpers.MakeDataGrid();
                                  static DataGridTextColumn MakeCol(string h, string b, double w, bool star = false) => ViewHelpers.MakeCol(h, b, w, star);
                                  static Button MakeButton(string t, string c) => ViewHelpers.MakeButton(t, c); }
    partial class FiltersView  { static Border MakeHeader(string t) => ViewHelpers.MakeHeader(t);
                                  static Button MakeButton(string t, string c) => ViewHelpers.MakeButton(t, c); }
    partial class SettingsView { static Border MakeHeader(string t) => ViewHelpers.MakeHeader(t);
                                  static Button MakeButton(string t, string c) => ViewHelpers.MakeButton(t, c); }
    partial class PlayersView  { static Border MakeHeader(string t) => ViewHelpers.MakeHeader(t);
                                  static DataGrid MakeDataGrid() => ViewHelpers.MakeDataGrid();
                                  static DataGridTextColumn MakeCol(string h, string b, double w, bool star = false) => ViewHelpers.MakeCol(h, b, w, star); }
}
