using NUnit.Framework;
using System;
using System.IO;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Tests of <see cref="FileLogDataSource"/>.
	/// </summary>
	[TestFixture]
	class FileLogDataSourceTests : BaseLogDataSourceTests
	{
		// Create provider.
		protected override ILogDataSourceProvider CreateProvider()
		{
			if (LogDataSourceProviders.TryFindProviderByName("File", out var provider) && provider != null)
				return provider;
			throw new AssertionException("Cannot find provider.");
		}


		// Prepare source.
		protected override void PrepareSource(ILogDataSourceProvider provider, string[] data, out LogDataSourceOptions options)
		{
			var filePath = this.CreateTestFile();
			using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
			for (var i = 0; i < data.Length; ++i)
			{
				if (i > 0)
					writer.WriteLine();
				writer.Write(data[i]);
			}
			options = new LogDataSourceOptions() { FileName = filePath };
		}
	}
}
