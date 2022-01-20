﻿using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSource"/> based-on <see cref="TcpListener"/>.
	/// </summary>
	class TcpServerLogDataSource : BaseLogDataSource
	{
		// Implmentation of text reader.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly Encoding encoding;
			bool isListenerStarted;
			readonly TcpListener listener;
			TextReader? reader;
			Socket? socket;
			readonly TcpServerLogDataSource source;

			// Constructor.
			public ReaderImpl(TcpServerLogDataSource source, TcpListener listener, Encoding encoding)
			{
				this.encoding = encoding;
				this.listener = listener;
				this.source = source;
			}

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				this.reader = null;
				if (this.socket != null)
				{
					this.source.Logger.LogWarning($"Close socket");
					Global.RunWithoutError(this.socket.Close);
				}
				if (this.isListenerStarted)
				{
					this.source.Logger.LogWarning($"Stop listening {this.listener.LocalEndpoint}");
					Global.RunWithoutError(this.listener.Stop);
				}
				base.Dispose(disposing);
			}

			// Implementations.
			public override string? ReadLine()
			{
				// wait for client
				if (!this.isListenerStarted)
				{
					this.source.Logger.LogWarning($"Start listening {this.listener.LocalEndpoint}");
					this.isListenerStarted = true;
					try
					{
						this.listener.ExclusiveAddressUse = true;
						this.listener.Start();
						this.socket = this.listener.AcceptSocket();
						this.source.Logger.LogWarning("Socket accepted");
						this.reader = new StreamReader(new NetworkStream(this.socket, false), this.encoding);
					}
					catch (Exception ex)
					{
						this.source.Logger.LogError(ex, "Failed to listen");
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
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			var options = this.CreationOptions;
			var endPoint = options.IPEndPoint.AsNonNull();
			var listener = new TcpListener(endPoint);
			reader = new ReaderImpl(this, listener, options.Encoding ?? Encoding.UTF8);
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore() => LogDataSourceState.ReadyToOpenReader;
	}
}
