using NUnit.Framework;
using System.IO;
using System.Net;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Tests of <see cref="LogDataSourceOptions"/>.
/// </summary>
[TestFixture]
class LogDataSourceOptionsTests
{
	/// <summary>
	/// Test for saving and loading <see cref="LogDataSourceOptions.IsLineAcrossDatagramsAllowed"/>.
	/// </summary>
	[Test]
	public void LineAcrossDatagramsSavingAndLoadingTest()
	{
		// save and load option which is enabled
		var options = new LogDataSourceOptions
		{
			IPEndPoint = new IPEndPoint(IPAddress.Loopback, 9999),
			IsLineAcrossDatagramsAllowed = true,
		};
		using (var jsonDocument = SaveToJson(options))
		{
			Assert.That(jsonDocument.RootElement.GetProperty(nameof(LogDataSourceOptions.IsLineAcrossDatagramsAllowed)).ValueKind, Is.EqualTo(JsonValueKind.True));
			var loadedOptions = LogDataSourceOptions.Load(jsonDocument.RootElement);
			Assert.That(loadedOptions.IsLineAcrossDatagramsAllowed);
			Assert.That(loadedOptions, Is.EqualTo(options));
			Assert.That(loadedOptions, Is.Not.EqualTo(new LogDataSourceOptions { IPEndPoint = options.IPEndPoint }));
		}

		// option which is disabled by default is not saved
		options.IsLineAcrossDatagramsAllowed = false;
		using (var jsonDocument = SaveToJson(options))
		{
			Assert.That(jsonDocument.RootElement.TryGetProperty(nameof(LogDataSourceOptions.IsLineAcrossDatagramsAllowed), out var jsonValue), Is.False, $"Unexpected value: {jsonValue}");
			Assert.That(LogDataSourceOptions.Load(jsonDocument.RootElement).IsLineAcrossDatagramsAllowed, Is.False);
		}
	}


	// Save options to JSON document.
	static JsonDocument SaveToJson(LogDataSourceOptions options)
	{
		// save options
		using var stream = new MemoryStream();
		using (var jsonWriter = new Utf8JsonWriter(stream))
			options.Save(jsonWriter);

		// parse JSON
		return JsonDocument.Parse(stream.ToArray());
	}
}
