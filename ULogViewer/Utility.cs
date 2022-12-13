using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Rendering;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Controls;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Provide utility functions.
/// </summary>
static partial class Utility
{
    // Create pattern to check whether the given regex can match all text or not.
    [GeneratedRegex(@"^\.[\*\+]{0,1}$")]
    private static partial Regex CreateAllMatchingPatternRegex();


    // Create regular expression for parsing base name.
    [GeneratedRegex("^(?<Name>.+)\\s+\\(\\d+\\)\\s*$")]
    private static partial Regex CreateBaseNameRegex();

    
    /// <summary>
    /// Export image to file.
    /// </summary>
    /// <param name="window">Window for showing dialog.</param>
    /// <returns>Task of image output.</returns>
    public static async Task<bool> ExportImage(Avalonia.Controls.Window window)
    {
        // select image
        var image = (IImage?)null;
        var resName = (string?)null;
        var width = 0;
        var height = 0;
        while (true)
        {
            // get resource name
            resName = await new TextInputDialog()
            {
                InitialText = resName,
                Message = "Name of image resource",
            }.ShowDialog(window);
            if (string.IsNullOrWhiteSpace(resName))
                return false;
            
            // load image
            var geometry = App.Current.FindResourceOrDefault<Geometry>($"Geometry/{resName}");
            if (geometry != null)
            {
                image = new DrawingImage(new GeometryDrawing().Also(it =>
                {
                    it.Brush = new SolidColorBrush(Color.FromArgb(255, 127, 127, 127));
                    it.Geometry = geometry;
                }));
            }
            else
                image = App.Current.FindResourceOrDefault<IImage>($"Image/{resName}");
            if (image == null)
                continue;
            
            // get size
            var sizeString = (string?)"64";
            while (true)
            {
                sizeString = await new TextInputDialog()
                {
                    InitialText = sizeString,
                    Message = "Dimension of output image",
                }.ShowDialog(window);
                if (string.IsNullOrWhiteSpace(sizeString))
                    break;
                var subSizeStrings = sizeString.Split('x');
                if (subSizeStrings.Length == 2)
                {
                    if (int.TryParse(subSizeStrings[0].Trim(), out width)
                        && int.TryParse(subSizeStrings[1].Trim(), out height)
                        && width > 0
                        && height > 0)
                    {
                        break;
                    }
                }
                else if (subSizeStrings.Length == 1)
                {
                    if (int.TryParse(sizeString.Trim(), out var size)
                        && size > 0)
                    {
                        width = size;
                        height = size;
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(sizeString))
                return false;
            break;
        }

        // select file
        var fileName = (await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions().Also(options =>
        {
            options.FileTypeChoices = new FilePickerFileType[]
            {
                new FilePickerFileType(App.Current.GetStringNonNull("FileFormat.Png")).Also(it =>
                {
                    it.Patterns = new string[] { "*.png" };
                })
            };
        })))?.Let(file =>
        {
            if (file.TryGetUri(out var uri))
                return uri.LocalPath;
            return null;
        });
        if (string.IsNullOrEmpty(fileName))
            return false;

        // export
        return await ExportImage(image, new(width, height), fileName);
    }


    /// <summary>
    /// Export image to file.
    /// </summary>
    /// <param name="image">Image.</param>
    /// <param name="outputSize">Output dimension.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of image output.</returns>
    public static async Task<bool> ExportImage(IImage image, PixelSize outputSize, string fileName)
    {
        if (outputSize.Width <= 0 || outputSize.Height <= 0)
            return false;
        try
        {
            // convert to bitmap
            var bitmap = new RenderTargetBitmap(outputSize, new(96, 96)).Also(bitmap =>
            {
                bitmap.CreateDrawingContext(new ImmediateRenderer(new Panel())).Use(idc =>
                {
                    using var dc = new DrawingContext(idc, false);
                    var srcSize = image.Size;
                    var srcSideLength = Math.Max(srcSize.Width, srcSize.Height);
                    image.Draw(dc, new((srcSize.Width - srcSideLength) / 2, (srcSize.Height - srcSideLength) / 2, srcSideLength, srcSideLength), new(0, 0, outputSize.Width, outputSize.Height), BitmapInterpolationMode.HighQuality);
                });
            });

            // save to file
            await Task.Run(() => bitmap.Save(fileName));

            // complete
            return true;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Generate a new which doesn't exist.
    /// </summary>
    /// <param name="baseName">Base name.</param>
    /// <param name="existingNameChecking">Function to check whether given name is existing or not.</param>
    /// <returns>Generated name.</returns>
    public static string GenerateName(string? baseName, Predicate<string> existingNameChecking)
    {
        baseName ??= "";
        baseName = CreateBaseNameRegex().Match(baseName).Let(it =>
            it.Success ? it.Groups["Name"].Value : baseName);
        if (!existingNameChecking(baseName))
            return baseName;
        for (var n = 2; n <= 100; ++n)
        {
            var candidateName = $"{baseName} ({n})";
            if (!existingNameChecking(candidateName))
                return candidateName;
        }
        return baseName;
    }


    /// <summary>
    /// Check whether given regex can match all strings or not.
    /// </summary>
    /// <param name="regex">Regex.</param>
    /// <returns>True if given regex can match all strings.</returns>
    public static bool IsAllMatchingRegex(Regex regex)
    {
        var pattern = regex.ToString();
        var patternLength = pattern.Length;
        var patternStart = 0;
        var subPatternBuffer = new StringBuilder();
        var bracketCount = 0;
        var allMatchingPatternRegex = default(Regex);
        while (patternStart < patternLength)
        {
            var c = pattern[patternStart++];
            switch (c)
            {
                case '|':
                    if (bracketCount == 0)
                    {
                        allMatchingPatternRegex ??= CreateAllMatchingPatternRegex();
                        if (subPatternBuffer.Length == 0 || allMatchingPatternRegex.IsMatch(subPatternBuffer.ToString()))
                            return true;
                        subPatternBuffer.Clear();
                        break;
                    }
                    goto default;
                case '\\':
                    subPatternBuffer.Append(c);
                    if (patternStart >= patternLength)
                        break;
                    c = pattern[patternStart++];
                    goto default;
                case '(':
                    ++bracketCount;
                    goto default;
                case ')':
                    --bracketCount;
                    goto default;
                default:
                    subPatternBuffer.Append(c);
                    break;
            }
        }
        if (bracketCount == 0)
        {
            allMatchingPatternRegex ??= CreateAllMatchingPatternRegex();
            return (subPatternBuffer.Length == 0 || allMatchingPatternRegex.IsMatch(subPatternBuffer.ToString()));
        }
        return false;
    }
}