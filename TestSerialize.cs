using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextElement), typeDiscriminator: "text")]
public abstract class OverlayElement { public string ElementType { get; set; } }
public class TextElement : OverlayElement { public TextElement() { ElementType = "text"; } public string CustomText { get; set; } = "hi"; }

public class Program {
    public static void Main() {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var list = new List<OverlayElement> { new TextElement() };
        Console.WriteLine(JsonSerializer.Serialize(list, opts));
    }
}
