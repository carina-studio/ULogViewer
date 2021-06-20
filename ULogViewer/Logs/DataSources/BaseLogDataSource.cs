using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataSource"/>.
	/// </summary>
	abstract class BaseLogDataSource : BaseDisposable, ILogDataSource
	{
		// Wrapper of text reader.
		class TextReaderWrapper : TextReader
		{
			// Fields.
			volatile int isClosed;
			readonly BaseLogDataSource source;
			readonly TextReader textReader;

			// Constructor.
			public TextReaderWrapper(BaseLogDataSource source, TextReader textReader)
			{
				this.source = source;
				this.textReader = textReader;
			}

			// Close.
			public override void Close()
			{
				if (Interlocked.Exchange(ref this.isClosed, 1) != 0)
					return;
				base.Close();
				this.source.SynchronizationContext.Post(source.OnReaderClosed);
			}

			// Implementations.
			public override int Peek() => this.textReader.Peek();
			public override int Read() => this.textReader.Read();
			public override int Read(char[] buffer, int index, int count) => this.textReader.Read(buffer, index, count);
			public override int ReadBlock(char[] buffer, int index, int count) => this.textReader.ReadBlock(buffer, index, count);
			public override int ReadBlock(Span<char> buffer) => this.textReader.ReadBlock(buffer);
			public override string? ReadLine() => this.textReader.ReadLine();
			public override string ReadToEnd() => this.textReader.ReadToEnd();
		}


		// Static fields.
		static int nextId = 1;


		// Fields.
		string? displayName;
		LogDataSourceState state = LogDataSourceState.Initializing;


		/// <summary>
		/// Initialize new <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/> which creates this instane.</param>
		protected BaseLogDataSource(ILogDataSourceProvider provider)
		{
			provider.VerifyAccess();
			this.Id = nextId++;
			this.Logger = provider.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.Provider = provider;
			this.SynchronizationContext.Post(this.Prepare);
		}


		/// <summary>
		/// Get <see cref="App"/> instance.
		/// </summary>
		public App Application { get => (App)this.Provider.Application; }


		// change state.
		LogDataSourceState ChangeState(LogDataSourceState state)
		{
			var prevState = this.state;
			if (prevState == state)
				return state;
			this.state = state;
			this.Logger.LogDebug($"Change state from {prevState} to {state}");
			this.OnPropertyChanged(nameof(State));
			return this.state;
		}


		/// <summary>
		/// Get or set name for displaying purpose.
		/// </summary>
		public string? DisplayName
		{
			get => this.displayName;
			protected set
			{
				this.VerifyAccess();
				if (this.IsDisposed || this.displayName == value)
					return;
				this.displayName = value;
				this.OnPropertyChanged(nameof(DisplayName));
			}
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			this.VerifyAccess();
			this.ChangeState(LogDataSourceState.Disposed);
			if (this.Provider is BaseLogDataSourceProvider baseProvider)
				baseProvider.NotifySourceDisposedInternal(this);
		}


		/// <summary>
		/// Get unique ID of this <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		protected int Id { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Called when opened reader has been closed.
		/// </summary>
		protected virtual void OnReaderClosed()
		{
			this.Logger.LogDebug("Reader closed");
			if (this.IsDisposed)
				return;
			if (this.ChangeState(LogDataSourceState.ClosingReader) != LogDataSourceState.ClosingReader)
				return;
			this.SynchronizationContext.Post(this.Prepare);
		}


		// Open reader asynchronously.
		public async Task<TextReader> OpenReaderAsync()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (this.State != LogDataSourceState.ReadyToOpenReader)
				throw new InvalidOperationException($"Cannot open reader when state is {this.State}.");

			// change state
			if (this.ChangeState(LogDataSourceState.OpeningReader) != LogDataSourceState.OpeningReader)
				throw new InternalStateCorruptedException("Internal state has been changed when opening reader.");

			// open reader
			var reader = await Task.Run(this.OpenReaderCore);
			if (this.state != LogDataSourceState.OpeningReader)
			{
				this.Logger.LogWarning($"State has been changed to {this.state} when opening reader");
				_ = Task.Run(() =>
				{
					try
					{
						reader.Dispose();
					}
					catch
					{ }
				});
				if (this.IsDisposed)
					throw new ObjectDisposedException("Source has been disposed when opening reader.");
				throw new InternalStateCorruptedException("Internal state has been changed when opening reader.");
			}
			if (this.ChangeState(LogDataSourceState.ReaderOpened) != LogDataSourceState.ReaderOpened)
			{
				_ = Task.Run(() =>
				{
					try
					{
						reader.Dispose();
					}
					catch
					{ }
				});
				throw new InternalStateCorruptedException("Internal state has been changed when opening reader.");
			}
			return new TextReaderWrapper(this, reader);
		}


		/// <summary>
		/// Open <see cref="TextReader"/> to read log data.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <returns><see cref="TextReader"/>.</returns>
		protected abstract TextReader OpenReaderCore();


		/// <summary>
		/// Start preparation.
		/// </summary>
		protected async void Prepare()
		{
			// check state
			this.VerifyAccess();
			switch (this.state)
			{
				case LogDataSourceState.Preparing:
					this.Logger.LogWarning("Start preparation while preparing");
					return;
				case LogDataSourceState.Initializing:
				case LogDataSourceState.ReadyToOpenReader:
				case LogDataSourceState.ClosingReader:
				case LogDataSourceState.SourceNotFound:
				case LogDataSourceState.UnclassifiedError:
					break;
				default:
					this.Logger.LogWarning($"Cannot start preparation when state is {this.state}");
					return;
			}

			// change state
			if (this.ChangeState(LogDataSourceState.Preparing) != LogDataSourceState.Preparing)
				return;

			// prepare
			var result = await Task.Run(() =>
			{
				try
				{
					return this.PrepareCore();
				}
				catch(Exception ex)
				{
					this.Logger.LogError(ex, "Error occurred while preparing");
					return LogDataSourceState.UnclassifiedError;
				}
			});
			if (this.state != LogDataSourceState.Preparing)
				return;

			// update state
			this.ChangeState(result switch
			{
				LogDataSourceState.ReadyToOpenReader => LogDataSourceState.ReadyToOpenReader,
				LogDataSourceState.SourceNotFound => LogDataSourceState.SourceNotFound,
				_ => LogDataSourceState.UnclassifiedError,
			});
		}


		/// <summary>
		/// Prepare source.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <returns>One of <see cref="LogDataSourceState.ReadyToOpenReader"/>, <see cref="LogDataSourceState.SourceNotFound"/>, <see cref="LogDataSourceState.UnclassifiedError"/></returns>
		protected abstract LogDataSourceState PrepareCore();


		// Get readable string.
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";


		// Interface implementations.
		public bool CheckAccess() => this.Provider.CheckAccess();
		IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public ILogDataSourceProvider Provider { get; }
		public SynchronizationContext SynchronizationContext => this.Provider.SynchronizationContext;
		public LogDataSourceState State { get => this.state; }
		public UnderlyingLogDataSource UnderlyingSource => this.Provider.UnderlyingSource;
	}
}
