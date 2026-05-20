using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace DiapStash_Plugin
{
    public class OverlayPreset
    {
        public double CardW { get; set; } = 800; 
        public double CardH { get; set; } = 200;
        public int TransitionType { get; set; } = 0; 
        public double TransitionDurationMs { get; set; } = 400;
        public bool UseRealData { get; set; } = true;
        public string CardBackgroundHex { get; set; } = "#FFFFFF"; // Default white
        
        public List<OverlayElement> Elements { get; set; } = new List<OverlayElement>();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(TextElement), typeDiscriminator: "text")]
    [JsonDerivedType(typeof(BarElement), typeDiscriminator: "bar")]
    [JsonDerivedType(typeof(ImageElement), typeDiscriminator: "image")]
    public abstract class OverlayElement
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string ElementType { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int ZIndex { get; set; }
    }

    public class TextElement : OverlayElement
    {
        public TextElement() { ElementType = "text"; }
        public string CustomText { get; set; } = "New Text";
        public string DataSource { get; set; } = "Custom"; // Custom, ProductName, Size, Wetness, Messiness, LiveStatus
        public string FontFamily { get; set; } = "Outfit";
        public double FontSize { get; set; } = 20;
        public string FontWeight { get; set; } = "Bold";
        public string FontStyle { get; set; } = "Normal"; // Normal, Italic
        public bool TextWrap { get; set; } = false;
        public string ColorHex { get; set; } = "#1E1E1E";
    }

    public class BarElement : OverlayElement
    {
        public BarElement() { ElementType = "bar"; }
        public string Orientation { get; set; } = "Horizontal"; // Horizontal, Vertical
        public string DataSource { get; set; } = "Wetness"; // Wetness, Messiness
        public string FillColorHex { get; set; } = "#0078D7";
        public string BgColorHex { get; set; } = "#E6E6E6";
    }

    public class ImageElement : OverlayElement
    {
        public ImageElement() { ElementType = "image"; }
        public string DataSource { get; set; } = "DiapStashImage"; // Custom, DiapStashImage
        public string CustomUrl { get; set; } = "";
        public string Stretch { get; set; } = "UniformToFill"; // Uniform, UniformToFill, Fill
    }
}
