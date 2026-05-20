using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public sealed partial class StreamingPage : UserControl
    {
        private bool _isInitialized = false;
        private FrameworkElement _selectedElement = null;
        private OverlayElement _selectedModel = null;
        private readonly string _presetPath = System.IO.Path.Combine(AppContext.BaseDirectory, "overlay_preset.json");
        private bool _previewMode = false;
        private DispatcherTimer _previewTimer = null;
        private bool _isRefreshingProperties = false;

        public StreamingPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => { OverlayServer.Instance.IsEditing = true; UpdateLocalPreview(); };
            this.Unloaded += (s, e) => { OverlayServer.Instance.IsEditing = false; };
            _isInitialized = true;
            LoadPreset();
            _ = SyncDesignWithOverlayServerAsync();
        }

        private void LoadPreset()
        {
            _isRefreshingProperties = true;
            bool presetLoaded = false;
            try
            {
                if (File.Exists(_presetPath))
                {
                    string json = File.ReadAllText(_presetPath);
                    var p = JsonSerializer.Deserialize<OverlayPreset>(json);
                    if (p != null)
                    {
                        CardWidthSlider.Value = p.CardW;
                        CardHeightSlider.Value = p.CardH;
                        if (CardWidthBox != null) CardWidthBox.Text = Math.Round(p.CardW).ToString();
                        if (CardHeightBox != null) CardHeightBox.Text = Math.Round(p.CardH).ToString();
                        TransitionTypeCombo.SelectedIndex = p.TransitionType;
                        TransitionSpeedSlider.Value = p.TransitionDurationMs;
                        CardBgColorPicker.Color = HexToColor(p.CardBackgroundHex);
                        OverlayServer.Instance.Elements = p.Elements;
                        presetLoaded = true;
                    }
                }
            }
            catch { }
            finally
            {
                _isRefreshingProperties = false;
            }

            if (!presetLoaded || OverlayServer.Instance.Elements == null || OverlayServer.Instance.Elements.Count == 0)
            {
                SetToDefaultDesign();
            }
            else
            {
                UpdateLocalPreview();
            }
        }

        private void SetToDefaultDesign()
        {
            _isRefreshingProperties = true;
            try
            {
                CardWidthSlider.Value = 800;
                CardHeightSlider.Value = 200;
                if (CardWidthBox != null) CardWidthBox.Text = "800";
                if (CardHeightBox != null) CardHeightBox.Text = "200";
                TransitionTypeCombo.SelectedIndex = 0; // Fade
                TransitionSpeedSlider.Value = 400;
                CardBgColorPicker.Color = Microsoft.UI.Colors.White;

                OverlayServer.Instance.Elements = new System.Collections.Generic.List<OverlayElement>
                {
                    new ImageElement { X = 30, Y = 30, Width = 120, Height = 120, ZIndex = 0, DataSource = "DiapStashImage" },
                    new TextElement { X = 170, Y = 30, Width = 400, Height = 40, ZIndex = 1, DataSource = "ProductName", FontFamily = "Outfit", FontSize = 28, FontWeight = "Bold", FontStyle = "Normal", ColorHex = "#FF1E1E1E" },
                    new TextElement { X = 170, Y = 75, Width = 200, Height = 30, ZIndex = 2, DataSource = "Size", FontFamily = "Outfit", FontSize = 18, FontWeight = "Normal", FontStyle = "Normal", ColorHex = "#FF7F7F7F" },
                    new BarElement { X = 170, Y = 115, Width = 250, Height = 16, ZIndex = 3, DataSource = "Wetness", FillColorHex = "#FF0078D7", BgColorHex = "#FFE6E6E6", Orientation = "Horizontal" },
                    new BarElement { X = 170, Y = 145, Width = 250, Height = 16, ZIndex = 4, DataSource = "Messiness", FillColorHex = "#FFE81123", BgColorHex = "#FFE6E6E6", Orientation = "Horizontal" }
                };

                _selectedElement = null;
                _selectedModel = null;
                RefreshPropertiesPanel();
                SavePreset();
                UpdateLocalPreview();
                _ = SyncDesignWithOverlayServerAsync();
            }
            finally
            {
                _isRefreshingProperties = false;
            }
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            SetToDefaultDesign();
        }

        private void SavePreset()
        {
            try
            {
                var p = new OverlayPreset
                {
                    CardW = CardWidthSlider.Value,
                    CardH = CardHeightSlider.Value,
                    TransitionType = TransitionTypeCombo.SelectedIndex,
                    TransitionDurationMs = TransitionSpeedSlider.Value,
                    CardBackgroundHex = ColorToHex(CardBgColorPicker.Color),
                    Elements = OverlayServer.Instance.Elements
                };
                File.WriteAllText(_presetPath, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // --- SPAWNING ---
        private void AddTextBtn_Click(object sender, RoutedEventArgs e)
        {
            var el = new TextElement { X = 50, Y = 50, Width = 200, Height = 40, ZIndex = OverlayServer.Instance.Elements.Count, CustomText = "New Text" };
            OverlayServer.Instance.Elements.Add(el);
            UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
        }
        private void AddBarBtn_Click(object sender, RoutedEventArgs e)
        {
            var el = new BarElement { X = 50, Y = 100, Width = 200, Height = 12, ZIndex = OverlayServer.Instance.Elements.Count };
            OverlayServer.Instance.Elements.Add(el);
            UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
        }
        private void AddImageBtn_Click(object sender, RoutedEventArgs e)
        {
            var el = new ImageElement { X = 50, Y = 150, Width = 100, Height = 100, ZIndex = OverlayServer.Instance.Elements.Count };
            OverlayServer.Instance.Elements.Add(el);
            UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
        }

        // --- RENDER ---
        private void UpdateLocalPreview()
        {
            if (WidgetArtboard != null)
            {
                WidgetArtboard.Width = CardWidthSlider.Value;
                WidgetArtboard.Height = CardHeightSlider.Value;
                WidgetArtboard.Background = new SolidColorBrush(HexToColor(ColorToHex(CardBgColorPicker.Color)));
            }

            var sg = SelectionGroup;
            EditorCanvas.Children.Clear();
            EditorCanvas.Children.Add(sg);
            ZOrderList.Items.Clear();

            var sorted = OverlayServer.Instance.Elements.OrderBy(x => x.ZIndex).ToList();
            foreach (var el in sorted)
            {
                FrameworkElement ui = null;
                string icon = ""; string name = "";

                if (el is TextElement te)
                {
                    icon = "📝"; name = te.DataSource == "Custom" ? "Text" : te.DataSource;
                    
                    string displayText = te.DataSource == "Custom" ? te.CustomText : $"[{te.DataSource}]";
                    if (_previewMode && te.DataSource != "Custom")
                    {
                        var s = OverlayServer.Instance;
                        if (te.DataSource == "ProductName") displayText = s.LiveProductName;
                        else if (te.DataSource == "Size") displayText = s.LiveSize;
                        else if (te.DataSource == "Wetness") displayText = s.LiveWetPercentage + "%";
                        else if (te.DataSource == "Messiness") displayText = s.LiveMessPercentage + "%";
                        else if (te.DataSource == "LiveStatus") displayText = s.LiveStatusMessage;
                    }

                    var weight = te.FontWeight == "Bold" ? Microsoft.UI.Text.FontWeights.Bold :
                                 te.FontWeight == "SemiBold" ? Microsoft.UI.Text.FontWeights.SemiBold :
                                 Microsoft.UI.Text.FontWeights.Normal;

                    var style = te.FontStyle == "Italic" ? Windows.UI.Text.FontStyle.Italic :
                                Windows.UI.Text.FontStyle.Normal;

                    ui = new TextBlock
                    {
                        Text = displayText,
                        FontSize = te.FontSize,
                        Foreground = new SolidColorBrush(HexToColor(te.ColorHex)),
                        FontWeight = weight,
                        FontStyle = style,
                        FontFamily = new FontFamily(te.FontFamily ?? "Outfit"),
                        TextWrapping = te.TextWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                }
                else if (el is BarElement be)
                {
                    icon = "📊"; name = be.DataSource + " Bar";
                    var bg = new Border { Background = new SolidColorBrush(HexToColor(be.BgColorHex)), CornerRadius = new CornerRadius(6) };
                    var fg = new Border { Background = new SolidColorBrush(HexToColor(be.FillColorHex)), CornerRadius = new CornerRadius(6) };
                    
                    double percentage = 0.5;
                    if (_previewMode)
                    {
                        if (be.DataSource == "Wetness") percentage = OverlayServer.Instance.LiveWetPercentage / 100.0;
                        else if (be.DataSource == "Messiness") percentage = OverlayServer.Instance.LiveMessPercentage / 100.0;
                    }

                    if (be.Orientation == "Horizontal") { fg.Width = el.Width * percentage; fg.HorizontalAlignment = HorizontalAlignment.Left; }
                    else { fg.Height = el.Height * percentage; fg.VerticalAlignment = VerticalAlignment.Bottom; }
                    var grid = new Grid(); grid.Children.Add(bg); grid.Children.Add(fg);
                    ui = grid;
                }
                else if (el is ImageElement ie)
                {
                    icon = "🖼️"; name = ie.DataSource == "Custom" ? "Image" : "DiapStash Avatar";
                    var b = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 0, 0)), CornerRadius = new CornerRadius(12), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 0, 0, 0)), BorderThickness = new Thickness(1) };
                    
                    string url = "";
                    if (ie.DataSource == "Custom")
                    {
                        url = ie.CustomUrl;
                    }
                    else
                    {
                        if (_previewMode) url = OverlayServer.Instance.LiveImageUrl;
                    }

                    var stretchMode = Stretch.UniformToFill;
                    if (ie.Stretch == "Uniform") stretchMode = Stretch.Uniform;
                    else if (ie.Stretch == "Fill") stretchMode = Stretch.Fill;

                    if (!string.IsNullOrEmpty(url)) {
                        try { b.Child = new Image { Source = new BitmapImage(new Uri(url)), Stretch = stretchMode }; } catch { }
                    } else {
                        b.Child = new FontIcon { Glyph = "\uEB9F", FontSize = 24, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(128, 0, 0, 0)) };
                    }
                    ui = b;
                }

                if (ui != null)
                {
                    ui.Width = el.Width;
                    ui.Height = el.Height;
                    Canvas.SetLeft(ui, el.X);
                    Canvas.SetTop(ui, el.Y);
                    Canvas.SetZIndex(ui, el.ZIndex);
                    ui.Tag = el;
                    ui.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                    ui.ManipulationDelta += Element_ManipulationDelta;
                    ui.PointerPressed += Element_PointerPressed;
                    EditorCanvas.Children.Add(ui);

                    var li = new ListViewItem { Content = $"{icon} {name}", Tag = el.Id };
                    ZOrderList.Items.Insert(0, li); // Reverse order for Z-Index display
                }
            }
            
            var cardItem = new ListViewItem { Content = "⬛ Card Properties", Tag = "CARD", FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            ZOrderList.Items.Insert(0, cardItem);

            if (_selectedModel != null) {
                _selectedElement = EditorCanvas.Children.FirstOrDefault(c => c is FrameworkElement f && f.Tag is OverlayElement o && o.Id == _selectedModel.Id) as FrameworkElement;
                UpdateSelectionBox();
            } else {
                SelectionGroup.Visibility = Visibility.Collapsed;
            }
        }

        // --- PROPERTIES ---
        private void Element_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            _selectedElement = sender as FrameworkElement;
            _selectedModel = _selectedElement?.Tag as OverlayElement;
            UpdateSelectionBox();
            RefreshPropertiesPanel();
        }

        private void EditorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _selectedElement = null; _selectedModel = null;
            SelectionGroup.Visibility = Visibility.Collapsed;
            RefreshPropertiesPanel();
        }

        private void RefreshPropertiesPanel()
        {
            _isRefreshingProperties = true;
            try
            {
                Properties_Global.Visibility = _selectedModel == null ? Visibility.Visible : Visibility.Collapsed;
                Properties_Text.Visibility = Visibility.Collapsed;
                Properties_Bar.Visibility = Visibility.Collapsed;
                Properties_Image.Visibility = Visibility.Collapsed;

                if (_selectedModel is TextElement te)
                {
                    Properties_Text.Visibility = Visibility.Visible;
                    TextDataSourceCombo.SelectedIndex = te.DataSource == "Custom" ? 0 : (te.DataSource == "ProductName" ? 1 : (te.DataSource == "Size" ? 2 : (te.DataSource == "Wetness" ? 3 : (te.DataSource == "Messiness" ? 4 : 5))));
                    TextCustomBox.Text = te.CustomText ?? "";
                    
                    // Hide custom input if not Custom
                    TextCustomBox.Visibility = te.DataSource == "Custom" ? Visibility.Visible : Visibility.Collapsed;

                    // Sync fonts dropdowns
                    TextFontFamilyCombo.SelectedIndex = te.FontFamily == "Outfit" ? 0 :
                                                        te.FontFamily == "Segoe UI" ? 1 :
                                                        te.FontFamily == "Arial" ? 2 :
                                                        te.FontFamily == "Consolas" ? 3 : 0;

                    TextFontWeightCombo.SelectedIndex = te.FontWeight == "Normal" ? 0 :
                                                        te.FontWeight == "SemiBold" ? 1 :
                                                        te.FontWeight == "Bold" ? 2 : 2;

                    TextFontStyleCombo.SelectedIndex = te.FontStyle == "Normal" ? 0 :
                                                       te.FontStyle == "Italic" ? 1 : 0;

                    TextSizeSlider.Value = te.FontSize;
                    TextWrapToggle.IsOn = te.TextWrap;
                    TextColorPicker.Color = HexToColor(te.ColorHex);
                }
                else if (_selectedModel is BarElement be)
                {
                    Properties_Bar.Visibility = Visibility.Visible;
                    BarDataSourceCombo.SelectedIndex = be.DataSource == "Wetness" ? 0 : 1;
                    BarOrientationCombo.SelectedIndex = be.Orientation == "Horizontal" ? 0 : 1;
                    BarFillColorPicker.Color = HexToColor(be.FillColorHex);
                    BarBgColorPicker.Color = HexToColor(be.BgColorHex);
                }
                else if (_selectedModel is ImageElement ie)
                {
                    Properties_Image.Visibility = Visibility.Visible;
                    ImageDataSourceCombo.SelectedIndex = ie.DataSource == "DiapStashImage" ? 0 : (ie.DataSource == "Custom" ? 1 : 0);
                    ImageCustomUrlBox.Text = ie.CustomUrl ?? "";
                    
                    // Hide custom URL box if not Custom
                    ImageCustomUrlBox.Visibility = ie.DataSource == "Custom" ? Visibility.Visible : Visibility.Collapsed;

                    ImageStretchCombo.SelectedIndex = ie.Stretch == "Uniform" ? 0 : (ie.Stretch == "UniformToFill" ? 1 : 2);
                }
                
                // Sync ZOrderList selection
                if (_selectedModel != null) {
                    var item = ZOrderList.Items.FirstOrDefault(i => (i as ListViewItem).Tag.ToString() == _selectedModel.Id);
                    if (item != null) ZOrderList.SelectedItem = item;
                } else { ZOrderList.SelectedItem = null; }
            }
            finally
            {
                _isRefreshingProperties = false;
            }
        }

        private void ElementProp_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _selectedModel == null || _isRefreshingProperties) return;
            
            if (_selectedModel is TextElement te) {
                te.DataSource = (TextDataSourceCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                te.CustomText = TextCustomBox.Text;
                
                te.FontFamily = (TextFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Outfit";
                te.FontWeight = (TextFontWeightCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Bold";
                te.FontStyle = (TextFontStyleCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Normal";

                te.FontSize = TextSizeSlider.Value;
                te.TextWrap = TextWrapToggle.IsOn;
                te.ColorHex = ColorToHex(TextColorPicker.Color);

                // Update custom input visibility
                TextCustomBox.Visibility = te.DataSource == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_selectedModel is BarElement be) {
                be.DataSource = (BarDataSourceCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                be.Orientation = (BarOrientationCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                be.FillColorHex = ColorToHex(BarFillColorPicker.Color);
                be.BgColorHex = ColorToHex(BarBgColorPicker.Color);
            }
            else if (_selectedModel is ImageElement ie) {
                ie.DataSource = (ImageDataSourceCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                ie.CustomUrl = ImageCustomUrlBox.Text;
                ie.Stretch = (ImageStretchCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

                // Update custom URL input visibility
                ImageCustomUrlBox.Visibility = ie.DataSource == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
        }

        // --- MANIPULATION ---
        private void Element_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is OverlayElement model)
            {
                model.X += e.Delta.Translation.X;
                model.Y += e.Delta.Translation.Y;
                Canvas.SetLeft(el, model.X);
                Canvas.SetTop(el, model.Y);
                UpdateSelectionBox(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
            }
        }

        private void ResizeHandle_DragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (_selectedElement != null && _selectedModel != null && sender is Microsoft.UI.Xaml.Controls.Primitives.Thumb thumb)
            {
                string tag = thumb.Tag?.ToString() ?? "BR";
                double minW = 10; double minH = 10;
                double dX = e.HorizontalChange; double dY = e.VerticalChange;

                if (tag.Contains("L")) { _selectedModel.Width = Math.Max(minW, _selectedModel.Width - dX); _selectedModel.X += (_selectedElement.Width - _selectedModel.Width); }
                else if (tag.Contains("R")) { _selectedModel.Width = Math.Max(minW, _selectedModel.Width + dX); }

                if (tag.Contains("T")) { _selectedModel.Height = Math.Max(minH, _selectedModel.Height - dY); _selectedModel.Y += (_selectedElement.Height - _selectedModel.Height); }
                else if (tag.Contains("B")) { _selectedModel.Height = Math.Max(minH, _selectedModel.Height + dY); }

                _selectedElement.Width = _selectedModel.Width; _selectedElement.Height = _selectedModel.Height;
                Canvas.SetLeft(_selectedElement, _selectedModel.X); Canvas.SetTop(_selectedElement, _selectedModel.Y);
                UpdateSelectionBox(); SavePreset(); _ = SyncDesignWithOverlayServerAsync();
            }
        }

        private void UpdateSelectionBox()
        {
            if (_selectedElement != null && _selectedModel != null)
            {
                SelectionGroup.Visibility = Visibility.Visible;
                SelectionGroup.Width = _selectedModel.Width + 10; 
                SelectionGroup.Height = _selectedModel.Height + 10;
                Canvas.SetLeft(SelectionGroup, _selectedModel.X - 5);
                Canvas.SetTop(SelectionGroup, _selectedModel.Y - 5);
            }
        }

        // --- Z-INDEX & ACTIONS ---
        private void BringForwardBtn_Click(object sender, RoutedEventArgs e) { if (_selectedModel != null) { _selectedModel.ZIndex++; UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync(); } }
        private void SendBackwardBtn_Click(object sender, RoutedEventArgs e) { if (_selectedModel != null) { _selectedModel.ZIndex = Math.Max(0, _selectedModel.ZIndex - 1); UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync(); } }
        private void DeleteElementBtn_Click(object sender, RoutedEventArgs e) { if (_selectedModel != null) { OverlayServer.Instance.Elements.Remove(_selectedModel); _selectedElement = null; _selectedModel = null; UpdateLocalPreview(); SavePreset(); _ = SyncDesignWithOverlayServerAsync(); } }

        private void ZOrderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZOrderList.SelectedItem is ListViewItem item)
            {
                if (item.Tag?.ToString() == "CARD")
                {
                    _selectedModel = null;
                    _selectedElement = null;
                    UpdateSelectionBox();
                    RefreshPropertiesPanel();
                }
                else if (item.Tag is string id)
                {
                    _selectedModel = OverlayServer.Instance.Elements.FirstOrDefault(x => x.Id == id);
                    if (_selectedModel != null) {
                        _selectedElement = EditorCanvas.Children.FirstOrDefault(c => c is FrameworkElement f && f.Tag == _selectedModel) as FrameworkElement;
                        UpdateSelectionBox(); RefreshPropertiesPanel();
                    }
                }
            }
        }

        // --- GLOBAL ---
        private void Control_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isRefreshingProperties) return;

            _isRefreshingProperties = true;
            try
            {
                if (ReferenceEquals(sender, CardWidthSlider) && CardWidthBox != null)
                {
                    CardWidthBox.Text = Math.Round(CardWidthSlider.Value).ToString();
                }
                else if (ReferenceEquals(sender, CardHeightSlider) && CardHeightBox != null)
                {
                    CardHeightBox.Text = Math.Round(CardHeightSlider.Value).ToString();
                }

                SavePreset();
                _ = SyncDesignWithOverlayServerAsync();
                UpdateLocalPreview();
            }
            finally
            {
                _isRefreshingProperties = false;
            }
        }

        private void CardDimensionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isRefreshingProperties) return;

            if (sender is TextBox tb && double.TryParse(tb.Text, out double val))
            {
                _isRefreshingProperties = true;
                try
                {
                    if (tb == CardWidthBox && CardWidthSlider != null)
                    {
                        double clamped = Math.Max(CardWidthSlider.Minimum, Math.Min(CardWidthSlider.Maximum, val));
                        CardWidthSlider.Value = clamped;
                    }
                    else if (tb == CardHeightBox && CardHeightSlider != null)
                    {
                        double clamped = Math.Max(CardHeightSlider.Minimum, Math.Min(CardHeightSlider.Maximum, val));
                        CardHeightSlider.Value = clamped;
                    }

                    SavePreset();
                    _ = SyncDesignWithOverlayServerAsync();
                    UpdateLocalPreview();
                }
                finally
                {
                    _isRefreshingProperties = false;
                }
            }
        }

        private void Color_Changed(ColorPicker sender, ColorChangedEventArgs args) => Control_Changed(sender, null);
        private void ElementPropColor_Changed(ColorPicker sender, ColorChangedEventArgs args) => ElementProp_Changed(sender, null);
        private void SaveBtn_Click(object sender, RoutedEventArgs e) { SavePreset(); _ = SyncDesignWithOverlayServerAsync(); }

        private async Task SyncDesignWithOverlayServerAsync()
        {
            var s = OverlayServer.Instance;
            s.CardW = CardWidthSlider.Value;
            s.CardH = CardHeightSlider.Value;
            s.TransitionType = TransitionTypeCombo.SelectedIndex;
            s.TransitionDurationMs = TransitionSpeedSlider.Value;
            s.CardBackgroundHex = ColorToHex(CardBgColorPicker.Color);

            var state = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();
            if (state != null)
            {
                s.LiveProductName = $"{state.ProductName}";
                s.LiveSize = state.Size;
                s.LiveWetPercentage = state.WetnessPercentage;
                s.LiveMessPercentage = state.MessyPercentage;
                s.LiveImageUrl = state.ImageUrl;
            }
        }

        private bool _isPanning = false;
        private Windows.Foundation.Point _panStartPos;

        private void WorkspaceGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(WorkspaceGrid);
            var delta = ptr.Properties.MouseWheelDelta;
            
            double zoomFactor = delta > 0 ? 1.1 : 1/1.1;
            double oldScale = ArtboardTransform.ScaleX;
            double newScale = oldScale * zoomFactor;
            
            if (newScale < 0.2) newScale = 0.2;
            if (newScale > 5.0) newScale = 5.0;
            
            zoomFactor = newScale / oldScale; // Recalculate true zoom in case of clamping

            var mousePos = ptr.Position;
            double centerX = WorkspaceGrid.ActualWidth / 2 + ArtboardTransform.TranslateX;
            double centerY = WorkspaceGrid.ActualHeight / 2 + ArtboardTransform.TranslateY;
            double dx = mousePos.X - centerX;
            double dy = mousePos.Y - centerY;
            
            ArtboardTransform.TranslateX += dx * (1 - zoomFactor);
            ArtboardTransform.TranslateY += dy * (1 - zoomFactor);
            ArtboardTransform.ScaleX = newScale;
            ArtboardTransform.ScaleY = newScale;
        }

        private void WorkspaceGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(WorkspaceGrid);
            if (ptr.Properties.IsMiddleButtonPressed || ptr.Properties.IsRightButtonPressed)
            {
                _isPanning = true;
                _panStartPos = ptr.Position;
                WorkspaceGrid.CapturePointer(e.Pointer);
            }
            else if (ptr.Properties.IsLeftButtonPressed)
            {
                _selectedElement = null; _selectedModel = null;
                SelectionGroup.Visibility = Visibility.Collapsed;
                RefreshPropertiesPanel();
            }
        }

        private void WorkspaceGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isPanning)
            {
                var ptr = e.GetCurrentPoint(WorkspaceGrid);
                ArtboardTransform.TranslateX += (ptr.Position.X - _panStartPos.X);
                ArtboardTransform.TranslateY += (ptr.Position.Y - _panStartPos.Y);
                _panStartPos = ptr.Position;
            }
        }

        private void WorkspaceGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                WorkspaceGrid.ReleasePointerCapture(e.Pointer);
            }
        }

        private void BackgroundGridCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var canvas = sender as Canvas;
            canvas.Children.Clear();
            var brush = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)); 
            
            for (double x = 0; x < canvas.ActualWidth; x += 40)
            {
                canvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = canvas.ActualHeight, Stroke = brush, StrokeThickness = 1 });
            }
            for (double y = 0; y < canvas.ActualHeight; y += 40)
            {
                canvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = canvas.ActualWidth, Y2 = y, Stroke = brush, StrokeThickness = 1 });
            }
        }

        private async void ObsHelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var stack = new StackPanel { Spacing = 12, Width = 450 };
            
            stack.Children.Add(new TextBlock { 
                Text = "Follow these steps to add the telemetry overlay to OBS Studio:", 
                TextWrapping = TextWrapping.Wrap, 
                Margin = new Thickness(0,0,0,4) 
            });

            var urlLabel = new TextBlock {
                Text = "Overlay URL",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 128, 128, 128))
            };
            stack.Children.Add(urlLabel);

            var urlPanel = new Grid { ColumnSpacing = 8 };
            urlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            urlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var urlBox = new TextBox { 
                Text = "http://localhost:8890/overlay/", 
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            urlPanel.Children.Add(urlBox);
            Grid.SetColumn(urlBox, 0);

            var copyBtn = new Button { 
                Content = "📋 Copy", 
                VerticalAlignment = VerticalAlignment.Center 
            };
            copyBtn.Click += (s, ev) => {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(urlBox.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                copyBtn.Content = "✅ Copied!";
                var queue = this.DispatcherQueue;
                Task.Run(async () => {
                    await Task.Delay(1500);
                    queue.TryEnqueue(() => { copyBtn.Content = "📋 Copy"; });
                });
            };
            urlPanel.Children.Add(copyBtn);
            Grid.SetColumn(copyBtn, 1);
            stack.Children.Add(urlPanel);

            var instLabel = new TextBlock {
                Text = "Setup Steps",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 128, 128, 128)),
                Margin = new Thickness(0,8,0,0)
            };
            stack.Children.Add(instLabel);

            var stepsPanel = new StackPanel { Spacing = 6 };
            
            var steps = new string[] {
                "1. Open OBS Studio, go to 'Sources' and click '+' to add a Browser Source.",
                "2. Name it (e.g., 'DiapStash Card') and click OK.",
                "3. Paste the copied Overlay URL into the URL field.",
                $"4. Set Width to {(int)CardWidthSlider.Value} and Height to {(int)CardHeightSlider.Value} (matches your current canvas dimensions).",
                "5. (Recommended) Check both 'Shutdown source when not visible' and 'Refresh browser when scene becomes active'.",
                "6. Click OK. Press the '🚀 Send to OBS' button in this app to test the layout animations."
            };

            foreach (var step in steps)
            {
                stepsPanel.Children.Add(new TextBlock { 
                    Text = step, 
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                });
            }
            stack.Children.Add(stepsPanel);

            var dialog = new ContentDialog
            {
                Title = "OBS Setup Instructions",
                Content = stack,
                CloseButtonText = "Done",
                XamlRoot = this.Content.XamlRoot
            };
            
            await dialog.ShowAsync();
        }
        private async void TtsHelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var stack = new StackPanel { Spacing = 12, Width = 480 };
            
            stack.Children.Add(new TextBlock { 
                Text = "JakeyTTS triggers this overlay automatically when mapped commands or channel rewards are executed.", 
                TextWrapping = TextWrapping.Wrap, 
                Margin = new Thickness(0,0,0,4) 
            });

            var recLabel = new TextBlock {
                Text = "Method 1: UI Trigger Plugin Column (Recommended)",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Margin = new Thickness(0,8,0,0)
            };
            stack.Children.Add(recLabel);

            var stepsPanel1 = new StackPanel { Spacing = 6 };
            var steps1 = new string[] {
                "1. Open JakeyTTS and navigate to the 'Commands' or 'Twitch Rewards' page.",
                "2. Find the command/reward you want to use (or create a new one).",
                "3. In the 'Trigger Plugin' column, click the dropdown and select:",
                "   👉 'diapstash_show (DiapStash Integration Bridge)'",
                "4. Click 'Save All Changes'. When this command/reward triggers on Twitch, the overlay will automatically slide in."
            };
            foreach (var step in steps1)
            {
                stepsPanel1.Children.Add(new TextBlock { 
                    Text = step, 
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                });
            }
            stack.Children.Add(stepsPanel1);

            var legacyLabel = new TextBlock {
                Text = "Method 2: TTS response variables (Alternative)",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Margin = new Thickness(0,10,0,0)
            };
            stack.Children.Add(legacyLabel);

            var stepsPanel2 = new StackPanel { Spacing = 6 };
            var steps2 = new string[] {
                "1. Add '{diapstash_show}' anywhere in a command or reward's TTS response text box.",
                "2. JakeyTTS will automatically filter out the brace tag (returning empty text for it to the speaker) and send a WebSocket event to show the DiapStash overlay."
            };
            foreach (var step in steps2)
            {
                stepsPanel2.Children.Add(new TextBlock { 
                    Text = step, 
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                });
            }
            stack.Children.Add(stepsPanel2);

            var dialog = new ContentDialog
            {
                Title = "JakeyTTS Setup Instructions",
                Content = stack,
                CloseButtonText = "Close",
                XamlRoot = this.Content.XamlRoot
            };
            
            await dialog.ShowAsync();
        }
        private void StartPreviewTimer()
        {
            if (_previewTimer == null)
            {
                _previewTimer = new DispatcherTimer();
                _previewTimer.Interval = TimeSpan.FromSeconds(6);
                _previewTimer.Tick += (s, e) =>
                {
                    _previewTimer.Stop();
                    _previewMode = false;
                    UpdateLocalPreview();
                };
            }
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void PlayLocalTransition()
        {
            double duration = TransitionSpeedSlider.Value;
            int type = TransitionTypeCombo.SelectedIndex;

            // Reset animation transform properties first
            AnimationTransform.ScaleX = 1.0;
            AnimationTransform.ScaleY = 1.0;
            AnimationTransform.TranslateX = 0.0;
            AnimationTransform.TranslateY = 0.0;
            WidgetArtboard.Opacity = 1.0;

            var sb = new Storyboard();
            var dur = TimeSpan.FromMilliseconds(duration);

            var opacityAnim = new DoubleAnimation { To = 1.0, Duration = dur };
            Storyboard.SetTarget(opacityAnim, WidgetArtboard);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            sb.Children.Add(opacityAnim);

            switch (type)
            {
                case 0: // Fade In
                    opacityAnim.From = 0.0;
                    break;
                case 1: // Zoom In
                    opacityAnim.From = 0.0;
                    var scaleX = new DoubleAnimation { From = 0.85, To = 1.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    var scaleY = new DoubleAnimation { From = 0.85, To = 1.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(scaleX, AnimationTransform);
                    Storyboard.SetTargetProperty(scaleX, "ScaleX");
                    Storyboard.SetTarget(scaleY, AnimationTransform);
                    Storyboard.SetTargetProperty(scaleY, "ScaleY");
                    sb.Children.Add(scaleX);
                    sb.Children.Add(scaleY);
                    break;
                case 2: // Slide Left
                    opacityAnim.From = 0.0;
                    var slideL = new DoubleAnimation { From = 100.0, To = 0.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(slideL, AnimationTransform);
                    Storyboard.SetTargetProperty(slideL, "TranslateX");
                    sb.Children.Add(slideL);
                    break;
                case 3: // Slide Right
                    opacityAnim.From = 0.0;
                    var slideR = new DoubleAnimation { From = -100.0, To = 0.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(slideR, AnimationTransform);
                    Storyboard.SetTargetProperty(slideR, "TranslateX");
                    sb.Children.Add(slideR);
                    break;
                case 4: // Slide Top
                    opacityAnim.From = 0.0;
                    var slideT = new DoubleAnimation { From = 100.0, To = 0.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(slideT, AnimationTransform);
                    Storyboard.SetTargetProperty(slideT, "TranslateY");
                    sb.Children.Add(slideT);
                    break;
                case 5: // Slide Bottom
                    opacityAnim.From = 0.0;
                    var slideB = new DoubleAnimation { From = -100.0, To = 0.0, Duration = dur, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(slideB, AnimationTransform);
                    Storyboard.SetTargetProperty(slideB, "TranslateY");
                    sb.Children.Add(slideB);
                    break;
                case 6: // Bounce Pop
                    opacityAnim.From = 0.0;
                    var bounceX = new DoubleAnimation { From = 0.5, To = 1.0, Duration = dur, EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
                    var bounceY = new DoubleAnimation { From = 0.5, To = 1.0, Duration = dur, EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
                    Storyboard.SetTarget(bounceX, AnimationTransform);
                    Storyboard.SetTargetProperty(bounceX, "ScaleX");
                    Storyboard.SetTarget(bounceY, AnimationTransform);
                    Storyboard.SetTargetProperty(bounceY, "ScaleY");
                    sb.Children.Add(bounceX);
                    sb.Children.Add(bounceY);
                    break;
            }

            sb.Begin();
        }

        private async void PreviewCanvasBtn_Click(object sender, RoutedEventArgs e)
        {
            await SyncDesignWithOverlayServerAsync();
            _previewMode = true;
            UpdateLocalPreview();
            PlayLocalTransition();
            StartPreviewTimer();
        }
        private async void LaunchObsBtn_Click(object sender, RoutedEventArgs e) { SavePreset(); await SyncDesignWithOverlayServerAsync(); OverlayServer.Instance.ForcePreviewTrigger = true; }
        
        private string ColorToHex(Windows.UI.Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        private Windows.UI.Color HexToColor(string hex) {
            try {
                hex = hex.Replace("#", "");
                if (hex.Length == 6) hex = "FF" + hex;
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return Windows.UI.Color.FromArgb(a, r, g, b);
            } catch { return Microsoft.UI.Colors.Transparent; }
        }
    }
}