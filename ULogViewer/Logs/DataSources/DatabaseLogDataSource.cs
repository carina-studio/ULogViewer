using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Base implementation of <see cref="ILogDataSource"/> based-on <see cref="DbConnection"/>.
/// </summary>
/// <typeparam name="TConnection">Type of database connection.</typeparam>
/// <typeparam name="TDataReader">Type of data reader.</typeparam>
abstract class DatabaseLogDataSource<TConnection, TDataReader> : BaseLogDataSource where TConnection : DbConnection where TDataReader : DbDataReader
{
	// Implementation of reader.
	class ReaderImpl(TDataReader reader) : TextReader
	{
		// Fields.
		int lineIndex;
		readonly List<string> lines = new();

		// Dispose.
		protected override void Dispose(bool disposing)
		{
			reader.Close();
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
				var dataReader = reader;
				if (!dataReader.Read())
					return null;
				var columnCount = dataReader.FieldCount;
				if (columnCount <= 0)
					return null;
				var lineBuffer = new StringBuilder();
				for (var i = 0; i < columnCount; ++i)
				{
					var name = dataReader.GetName(i);
					var value = dataReader.IsDBNull(i)
						? null
						: dataReader.GetValue(i);
					if (value == null)
						lines.Add($"<{name}></{name}>");
					else if (value is byte[] bytes)
						lines.Add($"<{name}>{Convert.ToBase64String(bytes)}</{name}>");
					else if (value is DateTime dateTime)
						lines.Add($"<{name}>{dateTime:yyyy/MM/dd HH:mm:ss.ffffff}</{name}>");
					else if (value is string str)
					{
						lineBuffer.Append($"<{name}>");
						str.Split('\n').Let(lines =>
						{
							for (var i = 0; i < lines.Length; ++i)
							{
								if (i > 0)
									lineBuffer.Append("&#10;");
								lineBuffer.Append(WebUtility.HtmlEncode(lines[i]));
							}
						});
						lineBuffer.Append($"</{name}>");
						lines.Add(lineBuffer.ToString());
						lineBuffer.Clear();
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
	/// Initialize new <see cref="DatabaseLogDataSource{TConnection, TDataReader}"/> instance.
	/// </summary>
	/// <param name="provider">Provider.</param>
	/// <param name="options">Options.</param>
	protected DatabaseLogDataSource(ILogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
	{ }


	/// <summary>
	/// Create database connection.
	/// </summary>
	/// <param name="options">Options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Database connection.</returns>
	protected abstract Task<TConnection> CreateConnectionAsync(LogDataSourceOptions options, CancellationToken cancellationToken);


	/// <summary>
	/// Create database data reader to read data.
	/// </summary>
	/// <param name="connection">Database connection.</param>
	/// <param name="options">Options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Data reader.</returns>
	protected abstract Task<TDataReader> CreateDataReaderAsync(TConnection connection, LogDataSourceOptions options, CancellationToken cancellationToken);


	// Dispose.
	protected override void Dispose(bool disposing)
	{
		this.TaskFactory.StartNew(() => Global.RunWithoutError(() => this.connection?.Close()), CancellationToken.None);
		base.Dispose(disposing);
	}


	// Open reader.
	protected override async Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
	{
		// check connection
		var connection = this.connection;
		if (connection == null)
			return (LogDataSourceState.UnclassifiedError, null);

		// get data reader
		TDataReader? dataReader;
		try
		{
			dataReader = await this.CreateDataReaderAsync(connection, this.CreationOptions, cancellationToken);
		}
		catch (Exception ex)
		{
			this.Logger.LogError(ex, "Unable to create data reader");
			_ = this.TaskFactory.StartNew(() => Global.RunWithoutError(connection.Close), CancellationToken.None);
			return (LogDataSourceState.UnclassifiedError, null);
		}

		// complete
		return (LogDataSourceState.ReaderOpened, new ReaderImpl(dataReader));
	}


	// Prepare.
	protected override async Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
	{
		if (this.connection != null)
			return LogDataSourceState.ReadyToOpenReader;
		var connection = await this.CreateConnectionAsync(this.CreationOptions, cancellationToken);
		try
		{
			await this.TaskFactory.StartNew(connection.Open ,cancellationToken);
			this.connection = connection;
			return LogDataSourceState.ReadyToOpenReader;
		}
		catch (Exception ex)
		{
			this.Logger.LogError(ex, "Unable to open database connection");
			_ = this.TaskFactory.StartNew(() => Global.RunWithoutError(() => connection.Close()), CancellationToken.None);
			return LogDataSourceState.UnclassifiedError;
		}
	}
}
