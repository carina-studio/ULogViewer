using CarinaStudio.Logging;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSource"/> based-on <see cref="TcpListener"/>.
/// </summary>
class TcpServerLogDataSource : BaseLogDataSource
{
	// Implementation of text reader.
	class ReaderImpl(TcpServerLogDataSource source, TcpListener listener, Encoding encoding) : TextReader
	{
		// Fields.
		bool isListenerStarted;
		TextReader? reader;
		Socket? socket;

		// Dispose.
		protected override void Dispose(bool disposing)
		{
			this.reader = null;
			if (this.socket != null)
			{
				source.Logger.LogWarning($"Close socket");
				Global.RunWithoutError(this.socket.Close);
			}
			if (this.isListenerStarted)
			{
				// ReSharper disable once ComplexObjectDestructuringProblem
				source.Logger.LogWarning("Stop listening {localEndpoint}", listener.LocalEndpoint);
				Global.RunWithoutError(listener.Stop);
			}
			base.Dispose(disposing);
		}

		// Implementations.
		public override string? ReadLine()
		{
			// wait for client
			if (!this.isListenerStarted)
			{
				// ReSharper disable once ComplexObjectDestructuringProblem
				source.Logger.LogWarning("Start listening {localEndpoint}", listener.LocalEndpoint);
				this.isListenerStarted = true;
				try
				{
					listener.ExclusiveAddressUse = true;
					listener.Start();
					this.socket = listener.AcceptSocket();
					source.Logger.LogWarning("Socket accepted");
					this.reader = new StreamReader(new NetworkStream(this.socket, false), encoding);
				}
				catch (Exception ex)
				{
					source.Logger.LogError(ex, "Failed to listen");
					return null;
				}
			}

			// check state
			if (this.reader == null)
				return null;

			// read line
			try
			{
				return this.reader.ReadLine();
			}
			catch
			{
				if (this.reader == null)
					return null;
				throw;
			}
		}
	}


	/// <summary>
	/// Initialize new <see cref="TcpServerLogDataSource"/> instance.
	/// </summary>
	/// <param name="provider">Provider.</param>
	/// <param name="options">Options.</param>
	public TcpServerLogDataSource(TcpServerLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
	{
		if (options.IPEndPoint == null)
			throw new ArgumentException("No IP endpoint specified.");
	}


	// Open reader.
	protected override Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
	{
		var options = this.CreationOptions;
		var endPoint = options.IPEndPoint.AsNonNull();
		var listener = new TcpListener(endPoint);
		return Task.FromResult<(LogDataSourceState, TextReader?)>((LogDataSourceState.ReaderOpened, new ReaderImpl(this, listener, options.Encoding ?? Encoding.UTF8)));
	}


	// Prepare.
	protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken) => 
		Task.FromResult(LogDataSourceState.ReadyToOpenReader);
}
