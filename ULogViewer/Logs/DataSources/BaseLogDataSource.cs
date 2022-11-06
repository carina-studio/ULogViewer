using CarinaStudio.AppSuite;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

			// Implementations.
			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				this.textReader.Dispose();
				if (Interlocked.Exchange(ref this.isClosed, 1) != 0)
					return;
				this.source.SynchronizationContext.Post(() => source.OnReaderClosed(this));
			}
			public override int Peek() => this.textReader.Peek();
			public override int Read() => this.textReader.Read();
			public override int Read(char[] buffer, int index, int count) => this.textReader.Read(buffer, index, count);
			public override int ReadBlock(char[] buffer, int index, int count) => this.textReader.ReadBlock(buffer, index, count);
			public override int ReadBlock(Span<char> buffer) => this.textReader.ReadBlock(buffer);
			public override string? ReadLine() => this.textReader.ReadLine();
			public override string ReadToEnd() => this.textReader.ReadToEnd();
		}


		// Static fields.
		static readonly TaskFactory defaultTaskFactory = new();
		static int nextId = 1;


		// Fields.
		string? displayName;
		TextReaderWrapper? openedReader;
		LogDataSourceState state = LogDataSourceState.Initializing;


		/// <summary>
		/// Initialize new <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/> which creates this instane.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create instance.</param>
		protected BaseLogDataSource(ILogDataSourceProvider provider, LogDataSourceOptions options)
		{
			provider.VerifyAccess();
			this.Id = nextId++;
			this.Logger = provider.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.Provider = provider;
			this.CreationOptions = options;
			this.SynchronizationContext.Post(this.Prepare);
		}


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get => (IULogViewerApplication)this.Provider.Application; }


		// change state.
		LogDataSourceState ChangeState(LogDataSourceState state)
		{
			var prevState = this.state;
			if (prevState == state)
				return state;
			this.state = state;
			this.Logger.LogDebug("Change state from {prevState} to {state}", prevState, state);
			this.OnPropertyChanged(nameof(State));
			if (this.Application.IsDebugMode)
				this.Logger.LogTrace("Complete changing state from {prevState} to {state}", prevState, state);
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
			if (!disposing)
				return; // In case of exception occurred in constructor
			this.VerifyAccess();
			this.ChangeState(LogDataSourceState.Disposed);
			this.openedReader?.Let(reader =>
			{
				this.Logger.LogWarning("Close opened reader because of disposing");
				this.openedReader = null;
				Global.RunWithoutErrorAsync(reader.Close);
				this.OnReaderClosed();
			});
			if (this.Provider is BaseLogDataSourceProvider baseProvider)
				baseProvider.NotifySourceDisposedInternal(this);
		}

		
		/// <inheritdoc/>
		public IEnumerable<ExternalDependency> ExternalDependencies => this.Provider.ExternalDependencies;


		/// <summary>
		/// Generate message.
		/// </summary>
		/// <remarks>The method can be called from any thread.</remarks>
		/// <param name="type">Type.</param>
		/// <param name="message">Message.</param>
		protected void GenerateMessage(LogDataSourceMessageType type, string message)
		{
			if (!this.CheckAccess())
			{
				this.SynchronizationContext.Post(() => this.GenerateMessage(type, message));
				return;
			}
			this.MessageGenerated?.Invoke(this, new LogDataSourceMessage(type, message));
		}


		/// <summary>
		/// Get unique ID of this <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		protected int Id { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <inheritdoc/>
		public event Action<ILogDataSource, LogDataSourceMessage>? MessageGenerated;


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		// Called when opened reader has been closed.
		void OnReaderClosed(TextReaderWrapper reader)
		{
			if (this.openedReader == reader)
			{
				this.Logger.LogDebug("Reader closed");
				this.openedReader = null;
				this.OnReaderClosed();
			}
			else
				this.Logger.LogWarning("Unknown reader closed");
		}


		/// <summary>
		/// Called when opened reader has been closed.
		/// </summary>
		protected virtual void OnReaderClosed()
		{
			if (this.IsDisposed)
				return;
			if (this.state != LogDataSourceState.ReaderOpened)
			{
				this.Logger.LogWarning("Reader closed when state is {state}", this.state);
				return;
			}
			else if (this.ChangeState(LogDataSourceState.ClosingReader) != LogDataSourceState.ClosingReader)
				return;
			this.SynchronizationContext.Post(this.Prepare);
		}


		// Open reader asynchronously.
		public async Task<TextReader> OpenReaderAsync(CancellationToken cancellationToken)
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
			var reader = (TextReader?)null;
			var openingResult = LogDataSourceState.UnclassifiedError;
			try
			{
				(openingResult, reader) = await this.OpenReaderCoreAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					this.Logger.LogWarning("Opening reader has been cancelled");
					Global.RunWithoutErrorAsync(() => reader?.Close());
					if (this.state == LogDataSourceState.OpeningReader)
						this.Prepare();
					if (ex is not TaskCanceledException)
						throw new TaskCanceledException();
				}
				else
				{
					this.Logger.LogError(ex, "Unable to open reader");
					Global.RunWithoutErrorAsync(() => reader?.Close());
					if (this.state == LogDataSourceState.OpeningReader)
						this.ChangeState(LogDataSourceState.UnclassifiedError);
				}
				throw;
			}
			if (this.state != LogDataSourceState.OpeningReader)
			{
				this.Logger.LogWarning("State has been changed to {state} when opening reader", this.state);
				Global.RunWithoutErrorAsync(() => reader?.Close());
				if (this.IsDisposed)
					throw new InvalidOperationException("Source has been disposed when opening reader.");
				throw new InternalStateCorruptedException("Internal state has been changed when opening reader.");
			}
			if (cancellationToken.IsCancellationRequested)
			{
				this.Logger.LogWarning("Opening reader has been cancelled");
				Global.RunWithoutErrorAsync(() => reader?.Close());
				this.Prepare();
				throw new TaskCanceledException();
			}
			switch (openingResult)
			{
				case LogDataSourceState.ReaderOpened:
					if (reader == null)
					{
						this.Logger.LogError("No reader opened");
						this.ChangeState(LogDataSourceState.UnclassifiedError);
						throw new InternalStateCorruptedException("No reader opened.");
					}
					break;
				case LogDataSourceState.SourceNotFound:
					Global.RunWithoutErrorAsync(() => reader?.Close());
					this.ChangeState(LogDataSourceState.SourceNotFound);
					throw new Exception("Cannot open reader because source cannot be found.");
				default:
					Global.RunWithoutErrorAsync(() => reader?.Close());
					this.ChangeState(LogDataSourceState.UnclassifiedError);
					throw new Exception("Unclassified error while opening reader.");
			}
			if (this.ChangeState(LogDataSourceState.ReaderOpened) != LogDataSourceState.ReaderOpened)
			{
				Global.RunWithoutErrorAsync(() => reader?.Close());
				throw new InternalStateCorruptedException("Internal state has been changed when opening reader.");
			}
			this.openedReader = new TextReaderWrapper(this, reader);
			return this.openedReader;
		}


		/// <summary>
		/// Open <see cref="TextReader"/> asynchronously to read log data.
		/// </summary>
		/// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel opening task.</param>
		/// <returns>Task of opening reader. The result should be one of <see cref="LogDataSourceState.ReaderOpened"/>, <see cref="LogDataSourceState.SourceNotFound"/>, <see cref="LogDataSourceState.UnclassifiedError"/></returns>
		protected abstract Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken);


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
				case LogDataSourceState.OpeningReader:
				case LogDataSourceState.ClosingReader:
				case LogDataSourceState.SourceNotFound:
				case LogDataSourceState.UnclassifiedError:
					break;
				default:
					this.Logger.LogWarning("Cannot start preparation when state is {state}", this.state);
					return;
			}

			// change state
			if (this.ChangeState(LogDataSourceState.Preparing) != LogDataSourceState.Preparing)
				return;
			
			// check external dependencies
			foreach (var extDep in this.ExternalDependencies)
			{
				await extDep.WaitForCheckingAvailability();
				if (this.state != LogDataSourceState.Preparing)
					return;
				if (extDep.State != ExternalDependencyState.Available)
				{
					this.Logger.LogError("External dependency '{id}' is unavailable", extDep.Id);
					this.ChangeState(LogDataSourceState.ExternalDependencyNotFound);
					return;
				}
			}

			// prepare
			var result = LogDataSourceState.UnclassifiedError;
			try
			{
				result = await this.PrepareCoreAsync(default);
			}
			catch(Exception ex)
			{
				this.Logger.LogError(ex, "Error occurred while preparing");
			}
			if (this.state != LogDataSourceState.Preparing)
				return;

			// update state
			this.ChangeState(result switch
			{
				LogDataSourceState.ExternalDependencyNotFound
				or LogDataSourceState.ReadyToOpenReader
				or LogDataSourceState.SourceNotFound => result,
				_ => LogDataSourceState.UnclassifiedError,
			});
		}


		/// <summary>
		/// Prepare source.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Task of preparation. The result should be one of <see cref="LogDataSourceState.ReadyToOpenReader"/>, <see cref="LogDataSourceState.SourceNotFound"/>, <see cref="LogDataSourceState.UnclassifiedError"/>.</returns>
		protected abstract Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken);


		/// <summary>
		/// Get <see cref="TaskFactory"/> for execution of internal actions.
		/// </summary>
		protected virtual TaskFactory TaskFactory { get => defaultTaskFactory; }


		// Get readable string.
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";


		// Interface implementations.
		public bool CheckAccess() => this.Provider.CheckAccess();
		public LogDataSourceOptions CreationOptions { get; }
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public ILogDataSourceProvider Provider { get; }
		public SynchronizationContext SynchronizationContext => this.Provider.SynchronizationContext;
		public LogDataSourceState State { get => this.state; }
	}
}
