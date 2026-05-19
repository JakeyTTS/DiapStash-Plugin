using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public sealed partial class StreamingPage : UserControl
    {
        private bool _isInitialized = false;
        private FrameworkElement _selectedElement = null;

        public StreamingPage()
        {
            this.InitializeComponent();
            _isInitialized = true;
            InitializeUIFromSettings();
        }

        // --- MANEJADORES DE AYUDA ---
        private async void ObsHelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog { Title = "How to add to OBS", Content = "URL: http://localhost:8890/overlay/", CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
            await dialog.ShowAsync();
        }

        private async void TtsHelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog { Title = "JakeyTTS Setup", Content = "GET: http://localhost:8890/overlay/trigger", CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
            await dialog.ShowAsync();
        }

        // --- LÓGICA DE SINCRONIZACIÓN ---
        private void SaveBtn_Click(object sender, RoutedEventArgs e) { _ = SyncDesignWithOverlayServerAsync(); }

        private async void PreviewCanvasBtn_Click(object sender, RoutedEventArgs e)
        {
            await SyncDesignWithOverlayServerAsync();
            UpdateLocalPreview();
            Storyboard sb = new Storyboard();
            DoubleAnimation fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(400) };
            Storyboard.SetTarget(fade, WidgetArtboard); Storyboard.SetTargetProperty(fade, "Opacity");
            sb.Children.Add(fade); sb.Begin();
        }

        private async void LaunchObsBtn_Click(object sender, RoutedEventArgs e)
        {
            await SyncDesignWithOverlayServerAsync();
            UpdateLocalPreview();
            OverlayServer.Instance.ForcePreviewTrigger = true;
        }

        // --- LÓGICA DEL LIENZO (ARRASTRAR Y REDIMENSIONAR) ---
        private void BackgroundGridCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { /* Rejilla visual opcional */ }

        private void Element_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (sender is FrameworkElement el)
            {
                Canvas.SetLeft(el, Canvas.GetLeft(el) + e.Delta.Translation.X);
                Canvas.SetTop(el, Canvas.GetTop(el) + e.Delta.Translation.Y);
                UpdateSelectionBox();
                _ = SyncDesignWithOverlayServerAsync();
            }
        }

        private void ResizeHandle_DragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (_selectedElement != null)
            {
                _selectedElement.Width = Math.Max(20, _selectedElement.ActualWidth + e.HorizontalChange);
                _selectedElement.Height = Math.Max(20, _selectedElement.ActualHeight + e.VerticalChange);
                UpdateSelectionBox();
                _ = SyncDesignWithOverlayServerAsync();
            }
        }

        private void Element_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            _selectedElement = sender as FrameworkElement;
            UpdateSelectionBox();
        }

        private void EditorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _selectedElement = null;
            SelectionBox.Visibility = Visibility.Collapsed;
            ResizeHandle.Visibility = Visibility.Collapsed;
        }

        private void UpdateSelectionBox()
        {
            if (_selectedElement != null)
            {
                SelectionBox.Visibility = Visibility.Visible;
                ResizeHandle.Visibility = Visibility.Visible;
                SelectionBox.Width = _selectedElement.ActualWidth + 10;
                SelectionBox.Height = _selectedElement.ActualHeight + 10;
                double left = Canvas.GetLeft(_selectedElement);
                double top = Canvas.GetTop(_selectedElement);
                Canvas.SetLeft(SelectionBox, left - 5);
                Canvas.SetTop(SelectionBox, top - 5);
                Canvas.SetLeft(ResizeHandle, left + _selectedElement.ActualWidth - 2);
                Canvas.SetTop(ResizeHandle, top + _selectedElement.ActualHeight - 2);
            }
        }

        // --- DATOS Y UI ---
        private void Control_Changed(object sender, RoutedEventArgs e) { if (_isInitialized) { _ = SyncDesignWithOverlayServerAsync(); UpdateLocalPreview(); } }
        private void Color_Changed(ColorPicker sender, ColorChangedEventArgs args) => Control_Changed(sender, null);
        private void WidgetLayersList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private async Task SyncDesignWithOverlayServerAsync()
        {
            var s = OverlayServer.Instance;
            s.CardW = CardWidthSlider.Value;
            s.CardH = CardHeightSlider.Value;
            s.TransitionType = TransitionTypeCombo.SelectedIndex;
            s.TransitionDurationMs = TransitionSpeedSlider.Value;
            s.Title = CustomTitleBox.Text;
            s.ProductName = ProductBox.Text;
            s.ShowWetBar = ShowWetToggle.IsOn;
            s.ShowMessBar = ShowMessToggle.IsOn;
            s.WetColor = ColorToHex(WetColorPicker.Color);
            s.MessColor = ColorToHex(MessColorPicker.Color);

            if (UseRealDataToggle.IsOn)
            {
                var state = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();
                if (state != null) { s.Title = "LIVE STATUS"; s.ProductName = $"{state.ProductName} ({state.Size})"; }
            }
        }

        private void UpdateLocalPreview()
        {
            WidgetArtboard.Width = CardWidthSlider.Value;
            WidgetArtboard.Height = CardHeightSlider.Value;
            PreviewTitle.Text = OverlayServer.Instance.Title;
            PreviewProduct.Text = OverlayServer.Instance.ProductName;
            PreviewWetBar.Visibility = ShowWetToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            PreviewMessBar.Visibility = ShowMessToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitializeUIFromSettings()
        {
            var s = OverlayServer.Instance;
            CustomTitleBox.Text = s.Title;
            ProductBox.Text = s.ProductName;
            ShowWetToggle.IsOn = s.ShowWetBar;
            ShowMessToggle.IsOn = s.ShowMessBar;
            CardWidthSlider.Value = s.CardW;
            CardHeightSlider.Value = s.CardH;
        }
        private string ColorToHex(Windows.UI.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}