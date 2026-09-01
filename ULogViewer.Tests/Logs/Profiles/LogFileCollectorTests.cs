using CarinaStudio.ULogViewer.IO;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Tests of <see cref="LogFileCollector"/>.
/// </summary>
[TestFixture]
class LogFileCollectorTests : ApplicationBasedTests
{
	// Constants.
	const int MaxFileCount = 256;


	// Static fields.
	static readonly string[] SyslogLines =
	[
		"Aug 26 01:02:03 localhost kernel: something happened",
		"Aug 26 01:02:04 localhost kernel: something else happened",
	];


	// Fields.
	string? testDirectoryPath;


	// Collect log files in given directory.
	Task<IList<string>> CollectAsync(string directoryPath, int maxFileCount) =>
		LogFileCollector.CollectAsync(this.Application, directoryPath, maxFileCount, CancellationToken.None);


	/// <summary>
	/// Test for collecting log files which contain binary data.
	/// </summary>
	[Test]
	public void CollectBinaryFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directory
			var directoryPath = this.CreateTestDirectory();
			var textFilePath = CreateTextFile(directoryPath, "a.log", SyslogLines);
			var gzipFilePath = CreateGZipFile(directoryPath, "b.log.gz", SyslogLines);
			CreateBinaryFile(directoryPath, "c.bin", [ 0x00, 0x01, 0x02, 0xFD, 0xFC, 0x7F ]);

			// binary data is not readable as text, the head of a gzip file is binary before it is decompressed
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.EqualTo(new[] { textFilePath, gzipFilePath }));
		});
	}


	/// <summary>
	/// Test for collecting log files in a directory which holds no log file.
	/// </summary>
	[Test]
	public void CollectEmptyDirectoryTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// an empty directory holds no log file
			var directoryPath = this.CreateTestDirectory();
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.Empty);

			// a root directory which cannot be listed is reported to the caller instead of being treated as an empty one
			var missingDirectoryPath = Path.Combine(directoryPath, "missing");
			try
			{
				await this.CollectAsync(missingDirectoryPath, MaxFileCount);
				throw new AssertionException("Inaccessible root directory should be reported.");
			}
			catch (Exception ex)
			{
				if (ex is AssertionException)
					throw;
				Assert.That(ex, Is.InstanceOf<IOException>());
			}
		});
	}


	/// <summary>
	/// Test for collecting log files with hidden files and hidden sub-directories.
	/// </summary>
	[Test]
	public void CollectHiddenEntriesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directory
			var directoryPath = this.CreateTestDirectory();
			var filePath = CreateTextFile(directoryPath, "a.log", SyslogLines);
			CreateTextFile(directoryPath, ".hidden.log", SyslogLines);
			var hiddenDirectoryPath = Path.Combine(directoryPath, ".hidden");
			Directory.CreateDirectory(hiddenDirectoryPath);
			CreateTextFile(hiddenDirectoryPath, "b.log", SyslogLines);

			// a hidden file is not a log file which user wants to read, a hidden directory is never walked into
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.EqualTo(new[] { filePath }));
		});
	}


	/// <summary>
	/// Test for collecting log files with a linked sub-directory which refers to its own ancestor.
	/// </summary>
	[Test]
	public void CollectLinkedDirectoriesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directory
			var directoryPath = this.CreateTestDirectory();
			var filePath = CreateTextFile(directoryPath, "a.log", SyslogLines);
			try
			{
				Directory.CreateSymbolicLink(Path.Combine(directoryPath, "link"), directoryPath);
			}
			catch (Exception ex)
			{
				Assert.Ignore($"Unable to create symbolic link for testing: {ex.Message}");
			}

			// walking into a linked directory may never end
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.EqualTo(new[] { filePath }));
		});
	}


	/// <summary>
	/// Test for collecting log files with marked logs info files and empty files.
	/// </summary>
	[Test]
	public void CollectMarkedLogsInfoFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directory
			var directoryPath = this.CreateTestDirectory();
			var filePath = CreateTextFile(directoryPath, "a.log", SyslogLines);
			CreateTextFile(directoryPath, "a.log.ulvmark", "{}");
			CreateTextFile(directoryPath, "b.log");

			// a marked logs info file belongs to ULogViewer itself, an empty file carries no log
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.EqualTo(new[] { filePath }));
		});
	}


	/// <summary>
	/// Test for collecting log files in sub-directories.
	/// </summary>
	[Test]
	public void CollectSubDirectoriesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directories
			var directoryPath = this.CreateTestDirectory();
			var filePath2 = CreateTextFile(directoryPath, "b.log", SyslogLines);
			var filePath1 = CreateTextFile(directoryPath, "a.log", SyslogLines);
			var subDirectoryPath = Path.Combine(directoryPath, "sub");
			Directory.CreateDirectory(subDirectoryPath);
			var filePath3 = CreateTextFile(subDirectoryPath, "c.log", SyslogLines);
			var nestedDirectoryPath = Path.Combine(subDirectoryPath, "nested");
			Directory.CreateDirectory(nestedDirectoryPath);
			var filePath4 = CreateTextFile(nestedDirectoryPath, "d.log", SyslogLines);

			// the directories are walked breadth-first, the log files of each directory are ordered by name
			Assert.That(await this.CollectAsync(directoryPath, MaxFileCount), Is.EqualTo(new[] { filePath1, filePath2, filePath3, filePath4 }));
		});
	}


	/// <summary>
	/// Test for collecting more log files than allowed.
	/// </summary>
	[Test]
	public void CollectTooManyFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare directory
			var directoryPath = this.CreateTestDirectory();
			for (var i = 0; i < 5; ++i)
				CreateTextFile(directoryPath, $"{i}.log", SyslogLines);

			// collecting exactly as many log files as allowed is not a rejection
			Assert.That((await this.CollectAsync(directoryPath, 5)).Count, Is.EqualTo(5));

			// one log file more than allowed is reported to the caller
			try
			{
				await this.CollectAsync(directoryPath, 4);
				throw new AssertionException("Too many log files should be reported.");
			}
			catch (Exception ex)
			{
				if (ex is AssertionException)
					throw;
				Assert.That(ex, Is.InstanceOf<TooManyFilesException>());
				Assert.That(((TooManyFilesException)ex).RootDirectoryName, Is.EqualTo(directoryPath));
				Assert.That(((TooManyFilesException)ex).MaxFileCount, Is.EqualTo(4));
			}
		});
	}


	// Create file which contains given bytes in given directory.
	static string CreateBinaryFile(string directoryPath, string fileName, byte[] bytes)
	{
		var filePath = Path.Combine(directoryPath, fileName);
		System.IO.File.WriteAllBytes(filePath, bytes);
		return filePath;
	}


	// Create file which contains given lines compressed by gzip in given directory.
	static string CreateGZipFile(string directoryPath, string fileName, string[] lines)
	{
		var filePath = Path.Combine(directoryPath, fileName);
		using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
		{
			using var gzipStream = new GZipStream(stream, CompressionMode.Compress);
			using var writer = new StreamWriter(gzipStream, Encoding.UTF8);
			foreach (var line in lines)
				writer.WriteLine(line);
		}
		return filePath;
	}


	// Create directory which holds the files generated for a single test.
	string CreateTestDirectory()
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		var directoryPath = Path.Combine(this.testDirectoryPath, Guid.NewGuid().ToString());
		Directory.CreateDirectory(directoryPath);
		return directoryPath;
	}


	// Create file which contains given lines of text in given directory.
	static string CreateTextFile(string directoryPath, string fileName, params string[] lines)
	{
		var filePath = Path.Combine(directoryPath, fileName);
		System.IO.File.WriteAllLines(filePath, lines);
		return filePath;
	}


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
}
