using System;
using System.Data.SQLite;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
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
				throw new ArgumentException("No file nsme specified.");
			if (!options.IsOptionSet(nameof(LogDataSourceOptions.QueryString)))
				throw new ArgumentException("No query string specified.");
		}


		// Create connection.
		protected override Task<SQLiteConnection> CreateConnectionAsync(LogDataSourceOptions options, CancellationToken cancellationToken)
		{
			// data source
			var connectionString = new StringBuilder("Data source='");
			if (options.Uri != null)
				connectionString.Append(options.Uri.ToString());
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
			return Task.FromResult(new SQLiteConnection(connectionString.ToString()));
		}


		// Create data reader.
		protected override async Task<SQLiteDataReader> CreateDataReaderAsync(SQLiteConnection connection, LogDataSourceOptions options, CancellationToken cancellationToken)
		{
			using var command = connection.CreateCommand();
			command.CommandText = options.QueryString;
			return (SQLiteDataReader)(await command.ExecuteReaderAsync(cancellationToken));
		}
	}
}
