using System;
using System.Data.SQLite;
using System.Text;

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
		{ }


		// Create connection.
		protected override SQLiteConnection CreateConnection(LogDataSourceOptions options)
		{
			// data source
			var connectionString = new StringBuilder("Data source='");
			if (options.Uri != null)
				connectionString.Append(options.Uri.ToString());
			else
				connectionString.Append(options.FileName);
			connectionString.Append("'");

			// password
			options.Password?.Let(it =>
			{
				if (it.Length > 0)
				{
					connectionString.Append(";Password='");
					connectionString.Append(it);
					connectionString.Append("'");
				}
			});

			// create connection
			return new SQLiteConnection(connectionString.ToString());
		}


		// Create data reader.
		protected override SQLiteDataReader CreateDataReader(SQLiteConnection connection, LogDataSourceOptions options)
		{
			using var command = connection.CreateCommand();
			command.CommandText = options.QueryString;
			return command.ExecuteReader();
		}
	}
}
