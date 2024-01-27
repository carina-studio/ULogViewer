using CarinaStudio.ULogViewer.Data;
using System;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSource"/> based-on SQLite database.
/// </summary>
class SQLiteLogDataSource : DatabaseLogDataSource<SQLiteConnection, SQLiteDataReader>
{
	/// <summary>
	/// Initialize new <see cref="SQLiteLogDataSource"/> instance.
	/// </summary>
	/// <param name="provider">Provider.</param>
	/// <param name="options">Options.</param>
	public SQLiteLogDataSource(SQLiteLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
	{
		if (!options.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
			throw new ArgumentException("No file name specified.");
		if (!options.IsOptionSet(nameof(LogDataSourceOptions.QueryString)))
			throw new ArgumentException("No query string specified.");
	}


	/// <inheritdoc/>
	protected override Task<SQLiteConnection> CreateConnectionAsync(LogDataSourceOptions options, CancellationToken cancellationToken)
	{
		// data source
		var connectionString = new StringBuilder("Data source='");
		if (options.Uri != null)
			connectionString.Append(options.Uri);
		else
			connectionString.Append(options.FileName);
		connectionString.Append('\'');

		// password
		options.Password?.Let(it =>
		{
			if (it.Length > 0)
			{
				connectionString.Append(";Password='");
				connectionString.Append(it);
				connectionString.Append('\'');
			}
		});

		// create connection
		return this.TaskFactory.StartNew(() =>
		{
			if (!File.Exists(options.FileName))
				throw new FileNotFoundException($"Database file '{options.FileName}' not found.");
			return new SQLiteConnection(connectionString.ToString());
		}, cancellationToken);
	}


	/// <inheritdoc/>
	protected override Task<SQLiteDataReader> CreateDataReaderAsync(SQLiteConnection connection, LogDataSourceOptions options, CancellationToken cancellationToken) => this.TaskFactory.StartNew(() =>
	{
		using var command = connection.CreateCommand().Also(it =>
		{
			it.CommandText = options.QueryString ?? "";
		});
		var reader = command.ExecuteReader();
		if (cancellationToken.IsCancellationRequested)
		{
			reader.Close();
			throw new TaskCanceledException();
		}
		return reader;
	}, cancellationToken);


	/// <inheritdoc/>
	protected override TaskFactory TaskFactory => SQLite.DatabaseTaskFactory;
}
