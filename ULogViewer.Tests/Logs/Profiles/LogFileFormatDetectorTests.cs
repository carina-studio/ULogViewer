using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Tests of <see cref="LogFileFormatDetector"/>.
/// </summary>
[TestFixture]
class LogFileFormatDetectorTests : ApplicationBasedTests
{
	// Static fields.
	static readonly string[] ApacheLines =
	[
		"127.0.0.1 - frank [10/Oct/2000:13:55:36 -0700] \"GET /apache_pb.gif HTTP/1.0\" 200 2326",
		"127.0.0.1 - alice [10/Oct/2000:13:55:37 -0700] \"GET /index.html HTTP/1.0\" 200 1043",
	];
	static readonly string[] JsonObjectStreamLines =
	[
		"{\"Timestamp\":\"2026-08-26T01:02:03.4567890Z\",\"Message\":\"Hello\"}",
		"{\"Timestamp\":\"2026-08-26T01:02:04.4567890Z\",\"Message\":\"Bye\"}",
	];
	static readonly string[] SyslogLines =
	[
		"Aug 26 01:02:03 localhost kernel: something happened",
		"Aug 26 01:02:04 localhost kernel: something else happened",
	];


	// Fields.
	string? testDirectoryPath;


	// Detect format of given log file.
	Task<LogFileFormatDetectionResult> DetectAsync(string filePath) =>
		LogFileFormatDetector.DetectAsync(this.Application, filePath, CancellationToken.None);


	/// <summary>
	/// Delete directory which contains files generated for testing.
	/// </summary>
	[OneTimeTearDown]
	public void DeleteTestDirectory()
	{
		if (this.testDirectoryPath is not null)
		{
			Global.RunWithoutError(() => Directory.Delete(this.testDirectoryPath, true));
			this.testDirectoryPath = null;
		}
	}


	/// <summary>
	/// Test for detecting format of file which contains binary data.
	/// </summary>
	[Test]
	public void DetectBinaryFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// a null byte never appears in text encoded in UTF-8
			var binaryFilePath = this.GenerateBinaryFile([ 0x00, 0x01, 0x02, 0xFD, 0xFC, 0x7F, 0x13, 0x42, 0x99, 0xA1 ]);
			Assert.That((await this.DetectAsync(binaryFilePath)).Format, Is.EqualTo(LogFileFormat.Binary));

			// text encoded in UTF-16 is full of null bytes, its byte-order mark keeps it away from being treated as binary data
			var utf16FilePath = this.GenerateTextFile(Encoding.Unicode, true, SyslogLines);
			Assert.That((await this.DetectAsync(utf16FilePath)).Format, Is.EqualTo(LogFileFormat.PlainText));

			// text encoded in UTF-32 as well
			var utf32FilePath = this.GenerateTextFile(new UTF32Encoding(false, true), true, SyslogLines);
			Assert.That((await this.DetectAsync(utf32FilePath)).Format, Is.EqualTo(LogFileFormat.PlainText));

			// binary data compressed by gzip is judged after it has been decompressed
			var gzipFilePath = this.GenerateGZipBinaryFile([ 0x00, 0x01, 0x02, 0xFD, 0xFC, 0x7F, 0x13, 0x42, 0x99, 0xA1 ]);
			Assert.That((await this.DetectAsync(gzipFilePath)).Format, Is.EqualTo(LogFileFormat.Binary));
		});
	}


	/// <summary>
	/// Test for detecting format of file which contains no data.
	/// </summary>
	[Test]
	public void DetectEmptyFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			var filePath = this.GenerateTextFile(Encoding.UTF8, false, []);
			var result = await this.DetectAsync(filePath);
			Assert.That(result.Format, Is.EqualTo(LogFileFormat.PlainText));
			Assert.That(result.Encoding, Is.EqualTo(Encoding.UTF8));
		});
	}


	/// <summary>
	/// Test for detecting format of file which is compressed by gzip.
	/// </summary>
	[Test]
	public void DetectGZipFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// a gzip file is only readable through the plain text path, even when it contains JSON data
			var filePath = this.GenerateGZipFile(JsonObjectStreamLines);
			Assert.That((await this.DetectAsync(filePath)).Format, Is.EqualTo(LogFileFormat.PlainText));
		});
	}


	/// <summary>
	/// Test for detecting format of file which contains JSON data.
	/// </summary>
	[Test]
	public void DetectJsonFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// an array of objects
			var arrayFilePath = this.GenerateTextFile(Encoding.UTF8, false, [ "[", "  { \"Message\": \"Hello\" },", "  { \"Message\": \"World\" }", "]" ]);
			Assert.That((await this.DetectAsync(arrayFilePath)).Format, Is.EqualTo(LogFileFormat.Json));

			// a stream of objects which carry no property recognized as CLEF
			var streamFilePath = this.GenerateTextFile(Encoding.UTF8, false, [ "{ \"Message\": \"Hello\" }", "{ \"Message\": \"World\" }" ]);
			Assert.That((await this.DetectAsync(streamFilePath)).Format, Is.EqualTo(LogFileFormat.Json));

			// a single object
			var objectFilePath = this.GenerateTextFile(Encoding.UTF8, false, [ "{ \"Message\": \"Hello\" }" ]);
			Assert.That((await this.DetectAsync(objectFilePath)).Format, Is.EqualTo(LogFileFormat.Json));
		});
	}


	/// <summary>
	/// Test for detecting format of file which contains plain text.
	/// </summary>
	[Test]
	public void DetectPlainTextFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// syslog
			var syslogFilePath = this.GenerateTextFile(Encoding.UTF8, false, SyslogLines);
			Assert.That((await this.DetectAsync(syslogFilePath)).Format, Is.EqualTo(LogFileFormat.PlainText));

			// Apache access log
			var apacheFilePath = this.GenerateTextFile(Encoding.UTF8, false, ApacheLines);
			Assert.That((await this.DetectAsync(apacheFilePath)).Format, Is.EqualTo(LogFileFormat.PlainText));
		});
	}


	/// <summary>
	/// Test for detecting encoding of text from the byte-order mark of file.
	/// </summary>
	[Test]
	public void DetectTextEncodingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// without byte-order mark
			Assert.That((await this.DetectAsync(this.GenerateTextFile(Encoding.UTF8, false, SyslogLines))).Encoding, Is.EqualTo(Encoding.UTF8));

			// UTF-8
			Assert.That((await this.DetectAsync(this.GenerateTextFile(Encoding.UTF8, true, SyslogLines))).Encoding, Is.EqualTo(Encoding.UTF8));

			// UTF-16, the format must still be detected through the byte-order mark instead of being read as binary noise
			var utf16FilePath = this.GenerateTextFile(Encoding.Unicode, true, JsonObjectStreamLines);
			var utf16Result = await this.DetectAsync(utf16FilePath);
			Assert.That(utf16Result.Encoding, Is.EqualTo(Encoding.Unicode));
			Assert.That(utf16Result.Format, Is.Not.EqualTo(LogFileFormat.PlainText));

			// UTF-16 big endian
			Assert.That((await this.DetectAsync(this.GenerateTextFile(Encoding.BigEndianUnicode, true, SyslogLines))).Encoding, Is.EqualTo(Encoding.BigEndianUnicode));

			// UTF-32
			Assert.That((await this.DetectAsync(this.GenerateTextFile(new UTF32Encoding(false, true), true, SyslogLines))).Encoding, Is.EqualTo(new UTF32Encoding(false, true)));
		});
	}


	/// <summary>
	/// Test for detecting format of Windows event log file.
	/// </summary>
	[Test]
	public void DetectWindowsEventLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// only the signature at the head of file is inspected, so no real event log file is needed
			var filePath = this.GenerateBinaryFile([ 0x45, 0x6C, 0x66, 0x46, 0x69, 0x6C, 0x65, 0x00, 0x01, 0x02, 0x03 ]);
			Assert.That((await this.DetectAsync(filePath)).Format, Is.EqualTo(LogFileFormat.WindowsEventLog));

			// a file which only shares a prefix of the signature is not an event log file
			var similarFilePath = this.GenerateBinaryFile([ 0x45, 0x6C, 0x66, 0x46, 0x69, 0x6C, 0x65, 0x21, 0x01, 0x02, 0x03 ]);
			Assert.That((await this.DetectAsync(similarFilePath)).Format, Is.Not.EqualTo(LogFileFormat.WindowsEventLog));
		});
	}


	// Generate file which contains given bytes.
	string GenerateBinaryFile(byte[] bytes)
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		return Tests.Random.CreateFileWithRandomName(this.testDirectoryPath).Use(stream =>
		{
			stream.Write(bytes, 0, bytes.Length);
			return stream.Name;
		});
	}


	// Generate file which contains given bytes compressed by gzip.
	string GenerateGZipBinaryFile(byte[] bytes)
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		var filePath = Path.Combine(this.testDirectoryPath, $"{Guid.NewGuid()}.gz");
		using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
		{
			using var gzipStream = new GZipStream(stream, CompressionMode.Compress);
			gzipStream.Write(bytes, 0, bytes.Length);
		}
		return filePath;
	}


	// Generate file which contains given lines compressed by gzip.
	string GenerateGZipFile(string[] lines)
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		var filePath = Path.Combine(this.testDirectoryPath, $"{Guid.NewGuid()}.gz");
		using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
		using var gzipStream = new GZipStream(stream, CompressionMode.Compress);
		using var writer = new StreamWriter(gzipStream, Encoding.UTF8);
		foreach (var line in lines)
			writer.WriteLine(line);
		return filePath;
	}


	// Generate file which contains given lines encoded by given encoding.
	string GenerateTextFile(Encoding encoding, bool writeByteOrderMark, string[] lines)
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		return Tests.Random.CreateFileWithRandomName(this.testDirectoryPath).Use(stream =>
		{
			// write byte-order mark
			if (writeByteOrderMark)
			{
				var preamble = encoding.GetPreamble();
				stream.Write(preamble, 0, preamble.Length);
			}

			// write lines
			foreach (var line in lines)
			{
				var bytes = encoding.GetBytes(line + "\n");
				stream.Write(bytes, 0, bytes.Length);
			}

			// complete
			return stream.Name;
		});
	}
}
