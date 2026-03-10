#!/usr/bin/env dotnet run

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Constants.
const string IconsFilePath = "ULogViewer/Styles/Icons.axaml";
const string IconPathInSvgPattern = "<path[\\s\\r\\n]+([\\w\\:]+=\"[^\"]*\"[\\s\\r\\n]+)*d=\"(?<Path>[^\"]+)\"";
const string IconPathInXamlPattern = "^\\s*<(StreamGeometry|PathGeometry)\\s+x:Key=\"Geometry/(?<Key>[^\"]+)\"(\\s+[\\w\\:]+=\"[^\"]*\")*>(?<Path>[^\\<]*)";

// check arguments
if (args.Length == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("No file or directory specified.");
    return;
}

// collect svg files
var defaultConsoleColor = Console.ForegroundColor;
var svgFilePaths = new HashSet<string>();
foreach (var path in args)
{
    if (File.Exists(path))
        svgFilePaths.Add(path);
    else if (Directory.Exists(path))
    {
        foreach (var filePath in Directory.EnumerateFiles(path, "*.svg"))
            svgFilePaths.Add(filePath);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Path '{path}' not found.");
    }
}
if (svgFilePaths.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("No SVG files found.");
    return;
}
Console.ForegroundColor = defaultConsoleColor;
Console.WriteLine($"{svgFilePaths.Count} SVG file(s) found.");

// extract icon paths
var iconPaths = new Dictionary<string, string>();
var iconPathPattern = new Regex(IconPathInSvgPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
var utf8 = new UTF8Encoding(false);
foreach (var svgFilePath in svgFilePaths)
{
    try
    {
        Console.ForegroundColor = defaultConsoleColor;
        Console.WriteLine($"Extract icon path from '{svgFilePath}'.");
        using var reader = new StreamReader(svgFilePath, utf8);
        var rawSvg = reader.ReadToEnd();
        var match = iconPathPattern.Match(rawSvg);
        if (match.Success)
        {
            var iconPath = match.Groups["Path"].Value;
            if (match.NextMatch()?.Success != true)
                iconPaths[Path.GetFileNameWithoutExtension(svgFilePath)] = iconPath;
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Invalid icon path in '{svgFilePath}'.");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Icon path not found in '{svgFilePath}'.");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error occurred while extracting icon path from '{svgFilePath}'. {ex.GetType().Name}: {ex.Message}");
    }
}
if (iconPaths.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("No icon paths extracted.");
    return;
}
Console.ForegroundColor = defaultConsoleColor;
Console.WriteLine($"{iconPaths.Count} icon path(s) extracted.");

// import paths of geometries
try
{
    // read all lines
    var lines = new List<string>();
    using (var reader = new StreamReader(IconsFilePath, utf8))
    {
        var line = reader.ReadLine();
        while (line is not null)
        {
            lines.Add(line);
            line = reader.ReadLine();
        }
    }
    Console.WriteLine($"{lines.Count} line(s) read from Icons.axaml.");
    
    // import and write lines
    var geometryPattern = new Regex(IconPathInXamlPattern);
    var importCount = 0;
    using (var writer = new StreamWriter(IconsFilePath, append: false, utf8))
    {
        foreach (var line in lines)
        {
            var match = geometryPattern.Match(line);
            if (match.Success)
            {
                var key = match.Groups["Key"].Value;
                if (iconPaths.TryGetValue(key, out var iconPath))
                {
                    Console.WriteLine($"Import icon '{key}'.");
                    var pathGroup = match.Groups["Path"];
                    ++importCount;
                    writer.Write(line[..pathGroup.Index]);
                    writer.Write(iconPath);
                    writer.WriteLine(line[(pathGroup.Index + pathGroup.Length)..]);
                } else
                    writer.WriteLine(line);
            }
            else
                writer.WriteLine(line);
        }
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Error occurred while importing icon paths. {ex.GetType().Name}: {ex.Message}");
}