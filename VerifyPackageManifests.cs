#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Verify links in package manifests listed in 'manifestFileNames' which are changed but not committed yet, or in the
// given manifest files.
//
// Usage:
//   dotnet run VerifyPackageManifests.cs [manifest file ...]
//
// For each manifest:
//   - Show the version described in the manifest.
//   - Page URI and package URIs must be reachable.
//   - SHA256 of each package must match the uploaded file. Packages are hosted on Cloudflare R2 which provides no
//     SHA256 of files, so each file is downloaded once to compute it.
//
// Links may be unavailable for a while right after uploading the packages, so failed requests are retried 'RetryCount'
// times with 'RetryInterval' milliseconds between them.

// Constants.
const int RetryCount = 3;
const int RetryInterval = 10000;

// package manifests to verify, relative to root of repository
string[] manifestFileNames =
[
    "PackageManifest-v2.json",
    "PackageManifest-Preview-v2.json",
];

// state
var errorCount = 0;
var remoteSha256s = new Dictionary<string, string>();
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ULogViewer-VerifyPackageManifests");
httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
httpClient.Timeout = TimeSpan.FromMinutes(10);

// find root of repository
string repoRootPath;
try
{
    repoRootPath = RunGit(Environment.CurrentDirectory, "rev-parse", "--show-toplevel").Trim();
}
catch (Exception ex)
{
    WriteLine(ConsoleColor.Red, $"Unable to find root of git repository. {ex.Message}");
    return 1;
}

// collect manifest files
var manifestPaths = new List<string>();
if (args.Length > 0)
{
    foreach (var path in args)
    {
        if (File.Exists(path))
            manifestPaths.Add(Path.GetFullPath(path));
        else
        {
            WriteLine(ConsoleColor.Red, $"File '{path}' not found.");
            ++errorCount;
        }
    }
}
else
{
    // select changed manifests
    foreach (var fileName in manifestFileNames)
    {
        var path = Path.Combine(repoRootPath, fileName);
        string status;
        try
        {
            status = RunGit(repoRootPath, "status", "--porcelain", "--", fileName);
        }
        catch (Exception ex)
        {
            WriteLine(ConsoleColor.Red, $"Unable to get status of '{fileName}'. {ex.Message}");
            return 1;
        }
        if (!File.Exists(path))
            Console.WriteLine($"Skip '{fileName}' because it is not found.");
        else if (status.Length == 0)
            Console.WriteLine($"Skip '{fileName}' because it has no uncommitted change.");
        else
            manifestPaths.Add(path);
    }

    // check changed manifests
    if (manifestPaths.Count == 0)
    {
        WriteLine(ConsoleColor.Yellow, "No changed package manifest found.");
        return 0;
    }
}

// verify manifests
foreach (var manifestPath in manifestPaths)
{
    Console.WriteLine();
    Console.WriteLine($"Verify '{Path.GetRelativePath(Environment.CurrentDirectory, manifestPath)}'.");
    try
    {
        await VerifyManifestAsync(manifestPath);
    }
    catch (Exception ex)
    {
        ReportError($"Error occurred while verifying manifest. {ex.GetType().Name}: {ex.Message}");
    }
}

// report result
Console.WriteLine();
if (errorCount > 0)
{
    WriteLine(ConsoleColor.Red, $"Verification failed: {errorCount} error(s).");
    return 1;
}
WriteLine(ConsoleColor.Green, "Verification passed.");
return 0;


// Compute SHA256 of file by downloading it. The result is cached because manifests may share the same packages.
async Task<string> ComputeRemoteSha256Async(string uri)
{
    // use cached hash
    if (remoteSha256s.TryGetValue(uri, out var cachedSha256))
        return cachedSha256;

    // download
    Console.WriteLine($"  Download '{uri}' to compute SHA256...");
    using var response = await SendWithRetryAsync(HttpMethod.Get, uri, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    // compute hash
    await using var stream = await response.Content.ReadAsStreamAsync(CancellationToken.None);
    var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, CancellationToken.None));

    // cache hash
    remoteSha256s[uri] = sha256;
    return sha256;
}


// Get property of JSON object as string, or null if the property is absent.
string? GetStringProperty(JsonElement element, string name) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;


// Check whether given URI is reachable or not.
async Task<bool> IsUriReachableAsync(string uri, string subject)
{
    try
    {
        using var response = await SendWithRetryAsync(HttpMethod.Head, uri, HttpCompletionOption.ResponseHeadersRead);
        if (response.IsSuccessStatusCode)
            return true;
        ReportError($"{subject}: '{uri}' is unreachable, status: {(int)response.StatusCode} {response.ReasonPhrase}.");
    }
    catch (Exception ex)
    {
        ReportError($"{subject}: '{uri}' is unreachable. {ex.GetType().Name}: {ex.Message}");
    }
    return false;
}


// Report error.
void ReportError(string message)
{
    ++errorCount;
    WriteLine(ConsoleColor.Red, $"  [error] {message}");
}


// Run git command and get its standard output.
string RunGit(string workingDirectory, params string[] arguments)
{
    // start process
    var startInfo = new ProcessStartInfo("git")
    {
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        StandardOutputEncoding = Encoding.UTF8,
        UseShellExecute = false,
        WorkingDirectory = workingDirectory,
    };
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");

    // read output
    var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException(errorTask.Result.Trim());
    return output;
}


// Send request, and retry if it fails because the resource may not be available right after publishing a release.
async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string uri, HttpCompletionOption completionOption)
{
    for (var retry = 0; ; ++retry)
    {
        // send request
        string failure;
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            var response = await httpClient.SendAsync(request, completionOption, CancellationToken.None);
            if (response.IsSuccessStatusCode || retry >= RetryCount)
                return response;
            failure = $"status: {(int)response.StatusCode} {response.ReasonPhrase}";
            response.Dispose();
        }
        catch (Exception ex) when (retry < RetryCount)
        {
            failure = $"{ex.GetType().Name}: {ex.Message}";
        }

        // wait for next retry
        Console.WriteLine($"  Retry '{uri}' in {RetryInterval / 1000}s ({retry + 1}/{RetryCount}), {failure}");
        await Task.Delay(RetryInterval, CancellationToken.None);
    }
}


// Verify single manifest.
async Task VerifyManifestAsync(string manifestPath)
{
    // parse manifest and show version
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
    var root = document.RootElement;
    Console.WriteLine($"  Name: {GetStringProperty(root, "Name")}");
    Console.WriteLine($"  Version: {GetStringProperty(root, "Version")}");
    Console.WriteLine($"  Informational version: {GetStringProperty(root, "InformationalVersion")}");

    // verify page URI
    var pageUri = GetStringProperty(root, "PageUri");
    if (pageUri is null)
        ReportError("'PageUri' is not specified.");
    else if (await IsUriReachableAsync(pageUri, "Page"))
        WriteLine(ConsoleColor.Green, $"  [ok] Page: {pageUri}");

    // verify packages
    if (!root.TryGetProperty("Packages", out var packages) || packages.ValueKind != JsonValueKind.Array)
    {
        ReportError("'Packages' is not specified.");
        return;
    }
    var packageIndex = 0;
    foreach (var package in packages.EnumerateArray())
    {
        // get fields
        var baseVersion = GetStringProperty(package, "BaseVersion");
        var sha256 = GetStringProperty(package, "SHA256");
        var uri = GetStringProperty(package, "Uri");
        var subject = $"#{packageIndex} {GetStringProperty(package, "OperatingSystem")}/{GetStringProperty(package, "Architecture")}";
        if (baseVersion is not null)
            subject += $" (from {baseVersion})";
        ++packageIndex;
        if (uri is null || sha256 is null)
        {
            ReportError($"{subject}: 'Uri' or 'SHA256' is not specified.");
            continue;
        }

        // check whether file is reachable or not
        if (!await IsUriReachableAsync(uri, subject))
            continue;

        // download file to compute SHA256
        string actualSha256;
        try
        {
            actualSha256 = await ComputeRemoteSha256Async(uri);
        }
        catch (Exception ex)
        {
            ReportError($"{subject}: unable to download '{uri}'. {ex.GetType().Name}: {ex.Message}");
            continue;
        }

        // compare SHA256
        if (string.Equals(sha256, actualSha256, StringComparison.OrdinalIgnoreCase))
            WriteLine(ConsoleColor.Green, $"  [ok] {subject}: {uri[(uri.LastIndexOf('/') + 1)..]}");
        else
            ReportError($"{subject}: SHA256 mismatch, manifest: {sha256}, actual: {actualSha256.ToUpperInvariant()}.");
    }
}


// Write line with given color.
void WriteLine(ConsoleColor color, string message)
{
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ResetColor();
}
