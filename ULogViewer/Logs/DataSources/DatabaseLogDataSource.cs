using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataSource"/> based-on <see cref="DbConnection"/>.
	/// </summary>
	/// <typeparam name="TConnection">Type of database connection.</typeparam>
	/// <typeparam name="TDataReader">Type of data reader.</typeparam>
	abstract class DatabaseLogDataSource<TConnection, TDataReader> : BaseLogDataSource where TConnection : DbConnection where TDataReader : DbDataReader
	{
		// Implementation of reader.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly TDataReader dataReader;
			int lineIndex = 0;
			readonly List<string> lines = new List<string>();

			// Constructor.
			public ReaderImpl(TDataReader dataReader) => this.dataReader = dataReader;

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				this.dataReader.Close();
				base.Dispose(disposing);
			}

			// Implementations.
			public override string? ReadLine()
			{
				// move to next record
				if (this.lineIndex >= this.lines.Count)
				{
					var lines = this.lines;
					lines.Clear();
					this.lineIndex = 0;
					var dataReader = this.dataReader;
					if (!dataReader.Read())
						return null;
					var columnCount = dataReader.FieldCount;
					if (columnCount <= 0)
						return null;
					for (var i = 0; i < columnCount; ++i)
					{
						var name = dataReader.GetName(i);
						var value = dataReader.IsDBNull(i)
							? null
							: dataReader.GetValue(i);
						var isStringValue = dataReader.GetFieldType(i).Let(it =>
						{
							if (it == typeof(string))
								return true;
							if(it == typeof(char[]))
							{
								if (value is char[] array)
									value = new string(array);
								return true;
							}
							return false;
						});
						if (value == null)
						{
							if (isStringValue)
							{
								lines.Add($"<{name}>");
								lines.Add($"</{name}>");
							}
							else
								lines.Add($"<{name}></{name}>");
						}
						else if (value is byte[] bytes)
							lines.Add($"<{name}>{Convert.ToBase64String(bytes)}</{name}>");
						else if (value is DateTime dateTime)
							lines.Add($"<{name}>{dateTime.ToString("yyyy/MM/dd HH:mm:ss.ffffff")}</{name}>");
						else if (value is string str)
						{
							lines.Add($"<{name}>");
							foreach (var line in str.Split('\n'))
								lines.Add(WebUtility.HtmlEncode(line));
							lines.Add($"</{name}>");
						}
						else
							lines.Add($"<{name}>{WebUtility.HtmlEncode(value.ToString() ?? "")}</{name}>");
					}
				}

				// return current line
				return this.lines[this.lineIndex++];
			}
		}


		// Fields.
		volatile TConnection? connection;


		/// <summary>
		/// Initialize new <see cref="DatabaseLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="options">Options.</param>
		protected DatabaseLogDataSource(ILogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
			if (options.Uri == null && string.IsNullOrWhiteSpace(options.FileName))
				throw new ArgumentException("No file name or URI of database specified.");
		}


		/// <summary>
		/// Create database connection.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns>Database connection.</returns>
		protected abstract TConnection CreateConnection(LogDataSourceOptions options);


		/// <summary>
		/// Create database data reader to read data.
		/// </summary>
		/// <param name="connection">Database connection.</param>
		/// <param name="options">Options.</param>
		/// <returns>Data reader.</returns>
		protected abstract TDataReader CreateDataReader(TConnection connection, LogDataSourceOptions options);


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			Global.RunWithoutErrorAsync(() => this.connection?.Dispose());
			base.Dispose(disposing);
		}


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			// check connection
			reader = null;
			var connection = this.connection;
			if (connection == null)
				return LogDataSourceState.UnclassifiedError;

			// get data reader
			var dataReader = (TDataReader?)null;
			try
			{
				dataReader = this.CreateDataReader(connection, this.CreationOptions);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to create data reader");
				return LogDataSourceState.UnclassifiedError;
			}

			// complete
			reader = new ReaderImpl(dataReader);
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore()
		{
			if (this.connection != null)
				return LogDataSourceState.ReadyToOpenReader;
			var connection = this.CreateConnection(this.CreationOptions);
			try
			{
				connection.Open();
				this.connection = connection;
				return LogDataSourceState.ReadyToOpenReader;
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to open database connection");
				Global.RunWithoutError(connection.Dispose);
				return LogDataSourceState.UnclassifiedError;
			}
		}
	}
}
