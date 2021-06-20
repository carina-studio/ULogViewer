using CarinaStudio.Threading;
using NUnit.Framework;
using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Base implementation of tests of <see cref="ILogDataSource"/>.
	/// </summary>
	abstract class BaseLogDataSourceTests<TProvider, TSource> : AppBasedTests where TProvider : ILogDataSourceProvider where TSource : ILogDataSource
	{

	}
}
