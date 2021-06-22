using CarinaStudio.Tests;
using CarinaStudio.Threading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Base implementation of tests of <see cref="ILogDataSource"/> and <see cref="ILogDataSourceProvider"/>.
	/// </summary>
	abstract class BaseLogDataSourceTests : AppBasedTests
	{
		// Fields.
		readonly System.Random random = new System.Random();
		string? testDirectoryPath;


		/// <summary>
		/// Create <see cref="ILogDataSourceProvider"/> instance.
		/// </summary>
		/// <returns><see cref="ILogDataSourceProvider"/>.</returns>
		protected abstract ILogDataSourceProvider CreateProvider();


		/// <summary>
		/// Generate empty file for testing.
		/// </summary>
		/// <returns>Path of generated file.</returns>
		[MethodImpl(MethodImplOptions.Synchronized)]
		protected string CreateTestFile()
		{
			if (this.testDirectoryPath == null)
				this.testDirectoryPath = this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
			while (true)
			{
				var filePath = Path.Combine(this.testDirectoryPath, this.GenerateRandomLine(8));
				if (!File.Exists(filePath))
					return File.Create(filePath).Use(_ => filePath);
			}
		}


		/// <summary>
		/// Test for create source.
		/// </summary>
		[Test]
		public void CreatingSourceTest()
		{
			this.TestOnApplicationThread(() =>
			{
				// prepare
				var provider = this.CreateProvider();

				// create 1st instance
				this.PrepareSource(provider, this.GenerateRandomLines(), out var options);
				using var source1 = provider.CreateSource(options);
				Assert.AreSame(provider, source1.Provider, "Provider reported by source is different.");
				Assert.AreEqual(options, source1.CreationOptions, "Options reported by source is different.");

				// create 2nd source
				try
				{
					this.PrepareSource(provider, this.GenerateRandomLines(), out options);
					using var source2 = provider.CreateSource(options);
					Assert.AreSame(provider, source2.Provider, "Provider reported by source is different.");
					Assert.AreEqual(options, source2.CreationOptions, "Options reported by source is different.");
				}
				catch (Exception ex)
				{
					if (ex is AssertionException)
						throw;
					if (provider.AllowMultipleSources)
						throw;
				}

				// dispose 1st source
				source1.Dispose();

				// create 3rd instance
				this.PrepareSource(provider, this.GenerateRandomLines(), out options);
				using var source3 = provider.CreateSource(options);
				Assert.AreSame(provider, source3.Provider, "Provider reported by source is different.");
				Assert.AreEqual(options, source3.CreationOptions, "Options reported by source is different.");
			});
		}


		/// <summary>
		/// Delete generated test directory.
		/// </summary>
		[OneTimeTearDown]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void DeleteTestDirectory()
		{
			if (this.testDirectoryPath != null)
			{
				Directory.Delete(this.testDirectoryPath, true);
				this.testDirectoryPath = null;
			}
		}


		/// <summary>
		/// Generate line of string with random content.
		/// </summary>
		/// <param name="lineLength">Length of string.</param>
		/// <returns>Line of string.</returns>
		protected string GenerateRandomLine(int lineLength = 256) => new string(new char[lineLength].Also(it =>
		{
			for (var i = lineLength - 1; i >= 0; --i)
			{
				var n = this.random.Next(0, 36);
				if (n < 10)
					it[i] = (char)('0' + n);
				else
					it[i] = (char)('a' + (n - 10));
			}
		}));


		/// <summary>
		/// Generate lines of string with random content.
		/// </summary>
		/// <param name="lineCount">Number of lines.</param>
		/// <param name="lineLength">Length of each line.</param>
		/// <returns>Lines of string.</returns>
		protected string[] GenerateRandomLines(int lineCount = 16, int lineLength = 256) => new string[lineCount].Also(lines =>
		{
			for (var i = lineCount - 1; i >= 0; --i)
				lines[i] = this.GenerateRandomLine(lineLength);
		});


		/// <summary>
		/// Prepare underlying data for creating <see cref="ILogDataSource"/>.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/>.</param>
		/// <param name="data">Log data.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create source.</param>
		protected abstract void PrepareSource(ILogDataSourceProvider provider, string[] data, out LogDataSourceOptions options);


		/// <summary>
		/// Test for reading data from source.
		/// </summary>
		[Test]
		public void ReadingFromSourceTest()
		{
			this.AsyncTestOnApplicationThread(async () =>
			{
				var provider = this.CreateProvider();
				for (var i = 0; i < 5; ++i)
					await this.ReadingFromSourceTest(provider);
			});
		}


		// Test for reading data from source.
		async Task ReadingFromSourceTest(ILogDataSourceProvider provider)
		{
			// prepare
			var lines = this.GenerateRandomLines();
			this.PrepareSource(provider, lines, out var options);
			using var source = provider.CreateSource(options);

			// wait for ready to open reader
			Assert.IsTrue(await source.WaitForPropertyAsync(nameof(ILogDataSource.State), LogDataSourceState.ReadyToOpenReader, 5000));

			// open reader
			var openReaderTask = source.OpenReaderAsync();
			Assert.AreEqual(LogDataSourceState.OpeningReader, source.State);
			using var reader = await openReaderTask;
			Assert.AreEqual(LogDataSourceState.ReaderOpened, source.State);

			// check data
			var readLines = new List<string>();
			var readLine = reader.ReadLine();
			while (readLine != null)
			{
				readLines.Add(readLine);
				readLine = reader.ReadLine();
			}
			Assert.AreEqual(lines.Length, readLines.Count);
			for (var i = lines.Length - 1; i >= 0; --i)
				Assert.AreEqual(lines[i], readLines[i]);
			Assert.AreEqual(LogDataSourceState.ReaderOpened, source.State);

			// close reader
			reader.Close();
			if (source.State == LogDataSourceState.ReaderOpened)
				Assert.IsTrue(await source.WaitForPropertyAsync(nameof(ILogDataSource.State), LogDataSourceState.ClosingReader, 5000));
			if (source.State == LogDataSourceState.ClosingReader)
				Assert.IsTrue(await source.WaitForPropertyAsync(nameof(ILogDataSource.State), LogDataSourceState.Preparing, 5000));
			else
				Assert.AreEqual(LogDataSourceState.Preparing, source.State);

			// dispose source
			source.Dispose();
			Assert.IsTrue(await source.WaitForPropertyAsync(nameof(ILogDataSource.State), LogDataSourceState.Disposed, 5000));
		}
	}
}
