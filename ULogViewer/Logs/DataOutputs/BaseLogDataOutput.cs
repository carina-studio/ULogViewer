using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataOutputs
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataOutput"/>.
	/// </summary>
	abstract class BaseLogDataOutput : BaseDisposable, ILogDataOutput
	{
		// Wrapper of TextWriter.
		class TextWriterWrapper : TextWriter
		{
			// Fields.
			volatile int isClosed;
			readonly BaseLogDataOutput output;
			public readonly TextWriter WrappedWriter;

			// Constructor.
			public TextWriterWrapper(BaseLogDataOutput output, TextWriter writer)
			{
				this.output = output;
				this.WrappedWriter = writer;
			}

			// Implementations.
			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				this.WrappedWriter.Dispose();
				if (Interlocked.Exchange(ref this.isClosed, 1) != 0)
					return;
				this.output.SynchronizationContext.Post(() => output.OnWriterClosed(this));
			}
			public override Encoding Encoding => this.WrappedWriter.Encoding;
			public override void Flush() => this.WrappedWriter.Flush();
			public override IFormatProvider FormatProvider => this.WrappedWriter.FormatProvider;
#pragma warning disable CS8765
			public override string NewLine 
			{ 
				get => this.WrappedWriter.NewLine;
				set => this.WrappedWriter.NewLine = value;
			}
#pragma warning restore CS8765
			public override void Write(char value) => this.WrappedWriter.Write(value);
			public override void Write(char[] buffer, int index, int count) => this.WrappedWriter.Write(buffer, index, count);
			public override void Write(ReadOnlySpan<char> buffer) => this.WrappedWriter.Write(buffer);
			public override void Write(string? value) => this.WrappedWriter.Write(value);
			public override void WriteLine(char[] buffer, int index, int count) => this.WrappedWriter.WriteLine(buffer, index, count);
			public override void WriteLine(ReadOnlySpan<char> buffer) => this.WrappedWriter.WriteLine(buffer);
			public override void WriteLine(string? value) => this.WrappedWriter.WriteLine(value);
		}


		// Static fields.
		static readonly TaskFactory defaultTaskFactory = new TaskFactory();
		static int nextId = 1;


		// Fields.
		TextWriterWrapper? openedWriter;
		LogDataOutputState state = LogDataOutputState.Initializing;


		/// <summary>
		/// Initialize new <see cref="BaseLogDataOutput"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		protected BaseLogDataOutput(IULogViewerApplication app)
		{
			app.VerifyAccess();
			this.Application = app;
			this.Id = nextId++;
			this.Logger = app.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.SynchronizationContext.Post(this.Prepare);
		}


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


		// change state.
		LogDataOutputState ChangeState(LogDataOutputState state)
		{
			var prevState = this.state;
			if (prevState == state)
				return state;
			this.state = state;
			this.Logger.LogDebug($"Change state from {prevState} to {state}");
			this.OnPropertyChanged(nameof(State));
			return this.state;
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.VerifyAccess();
				this.ChangeState(LogDataOutputState.Disposed);
			}
			else
				return;
			this.openedWriter?.Let(writer =>
			{
				this.Logger.LogWarning("Close opened writer because of disposing");
				this.openedWriter = null;
				Global.RunWithoutErrorAsync(writer.Close);
				this.SynchronizationContext.Post(() => this.OnWriterClosed(writer.WrappedWriter));
			});
		}


		/// <summary>
		/// Get unique ID of this <see cref="BaseLogDataOutput"/> instance.
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


		// Called when writer closed.
		void OnWriterClosed(TextWriterWrapper writer)
		{
			if (this.openedWriter == writer)
			{
				this.Logger.LogDebug("Writer closed");
				this.openedWriter = null;
				this.OnWriterClosed(writer.WrappedWriter);
			}
			else
				this.Logger.LogWarning("Unknown writer closed");
		}


		/// <summary>
		/// Called when opened <see cref="TextWriter"/> has been closed.
		/// </summary>
		/// <param name="writer">Closed <see cref="TextWriter"/>.</param>
		protected virtual void OnWriterClosed(TextWriter writer)
		{
			if (this.IsDisposed)
				return;
			if (this.state != LogDataOutputState.WriterOpened)
			{
				this.Logger.LogWarning($"Writer closed when state is {this.state}");
				return;
			}
			else if (this.ChangeState(LogDataOutputState.ClosingWriter) != LogDataOutputState.ClosingWriter)
				return;
			this.SynchronizationContext.Post(this.Prepare);
		}


		// Open writer.
		public async Task<TextWriter> OpenWriterAsync()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (this.State != LogDataOutputState.ReadyToOpenWriter)
				throw new InvalidOperationException($"Cannot open writer when state is {this.State}.");

			// change state
			if (this.ChangeState(LogDataOutputState.OpeningWriter) != LogDataOutputState.OpeningWriter)
				throw new InternalStateCorruptedException("Internal state has been changed when opening writer.");

			// open reader
			var writer = (TextWriter?)null;
			var openingResult = LogDataOutputState.UnclassifiedError;
			try
			{
				openingResult = await this.TaskFactory.StartNew(() => this.OpenWriterCore(out writer));
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to open writer");
				Global.RunWithoutErrorAsync(() => writer?.Close());
				this.ChangeState(LogDataOutputState.UnclassifiedError);
				throw;
			}
			if (this.state != LogDataOutputState.OpeningWriter)
			{
				this.Logger.LogWarning($"State has been changed to {this.state} when opening writer");
				Global.RunWithoutErrorAsync(() => writer?.Close());
				if (this.IsDisposed)
					throw new InvalidOperationException("Source has been disposed when opening writer.");
				throw new InternalStateCorruptedException("Internal state has been changed when opening writer.");
			}
			switch (openingResult)
			{
				case LogDataOutputState.WriterOpened:
					if (writer == null)
					{
						this.Logger.LogError("No writer opened");
						this.ChangeState(LogDataOutputState.UnclassifiedError);
						throw new InternalStateCorruptedException("No writer opened.");
					}
					break;
				case LogDataOutputState.TargetNotFound:
					Global.RunWithoutErrorAsync(() => writer?.Close());
					this.ChangeState(LogDataOutputState.TargetNotFound);
					throw new Exception("Cannot open writer because target cannot be found.");
				default:
					Global.RunWithoutErrorAsync(() => writer?.Close());
					this.ChangeState(LogDataOutputState.UnclassifiedError);
					throw new Exception("Unclassified error while opening writer.");
			}
			if (this.ChangeState(LogDataOutputState.WriterOpened) != LogDataOutputState.WriterOpened)
			{
				Global.RunWithoutErrorAsync(() => writer?.Close());
				throw new InternalStateCorruptedException("Internal state has been changed when opening writer.");
			}
			this.openedWriter = new TextWriterWrapper(this, writer);
			return this.openedWriter;
		}


		/// <summary>
		/// Open <see cref="TextWriter"/> to write raw log data.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <param name="writer">Opened <see cref="TextWriter"/>.</param>
		/// <returns>One of <see cref="LogDataOutputState.WriterOpened"/>, <see cref="LogDataOutputState.TargetNotFound"/>, <see cref="LogDataOutputState.UnclassifiedError"/></returns>
		protected abstract LogDataOutputState OpenWriterCore(out TextWriter? writer);


		/// <summary>
		/// Start preparation.
		/// </summary>
		protected async void Prepare()
		{
			// check state
			this.VerifyAccess();
			switch (this.state)
			{
				case LogDataOutputState.Preparing:
					this.Logger.LogWarning("Start preparation while preparing");
					return;
				case LogDataOutputState.Initializing:
				case LogDataOutputState.ReadyToOpenWriter:
				case LogDataOutputState.ClosingWriter:
				case LogDataOutputState.TargetNotFound:
				case LogDataOutputState.UnclassifiedError:
					break;
				default:
					this.Logger.LogWarning($"Cannot start preparation when state is {this.state}");
					return;
			}

			// change state
			if (this.ChangeState(LogDataOutputState.Preparing) != LogDataOutputState.Preparing)
				return;

			// prepare
			var result = await this.TaskFactory.StartNew(() =>
			{
				try
				{
					return this.PrepareCore();
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Error occurred while preparing");
					return LogDataOutputState.UnclassifiedError;
				}
			});
			if (this.state != LogDataOutputState.Preparing)
				return;

			// update state
			this.ChangeState(result switch
			{
				LogDataOutputState.ReadyToOpenWriter => result,
				LogDataOutputState.TargetNotFound => result,
				_ => LogDataOutputState.UnclassifiedError,
			});
		}


		/// <summary>
		/// Prepare output.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <returns>One of <see cref="LogDataOutputState.ReadyToOpenWriter"/>, <see cref="LogDataOutputState.TargetNotFound"/>, <see cref="LogDataOutputState.UnclassifiedError"/>.</returns>
		protected abstract LogDataOutputState PrepareCore();


		/// <summary>
		/// Get <see cref="TaskFactory"/> for execution of internal actions.
		/// </summary>
		protected virtual TaskFactory TaskFactory { get => defaultTaskFactory; }


		// Get readable string.
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public LogDataOutputState State { get => this.state; }
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
	}
}
