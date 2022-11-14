using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Base implementation of <see cref="ILogWriter"/>.
	/// </summary>
	abstract class BaseLogWriter : BaseDisposable, ILogWriter
	{
		// Static fields.
		static int nextId = 1;


		// Fields.
		IList<Log> logs = Array.Empty<Log>();
		LogWriterState state = LogWriterState.Preparing;
		readonly CancellationTokenSource writingLogsCancellationTokenSource = new();


		/// <summary>
		/// Initialize new <see cref="BaseLogWriter"/> instance.
		/// </summary>
		/// <param name="dataOutput"><see cref="ILogDataOutput"/> to output raw log data.</param>
		protected BaseLogWriter(ILogDataOutput dataOutput)
		{
			// check state
			dataOutput.VerifyAccess();
			if (dataOutput.State == LogDataOutputState.Disposed)
				throw new ArgumentException("Output has been disposed.");

			// setup properties.
			this.Application = dataOutput.Application;
			this.DataOutput = dataOutput;
			this.Id = nextId++;
			this.Logger = dataOutput.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");

			// attach to output
			dataOutput.PropertyChanged += this.OnDataOutputPropertyChanged;
		}


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


		// Change state.
		bool ChangeState(LogWriterState state)
		{
			var prevState = this.state;
			if (prevState == state)
				return true;
			this.state = state;
			this.Logger.LogDebug("Change state from {prevState} to {state}", prevState, state);
			this.OnPropertyChanged(nameof(State));
			return (this.state == state);
		}


		/// <summary>
		/// Get <see cref="ILogDataOutput"/> to output raw log data.
		/// </summary>
		public ILogDataOutput DataOutput { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check thread
			if (!disposing)
				return;
			this.VerifyAccess();

			// cancel writing logs
			this.writingLogsCancellationTokenSource.Cancel();

			// detach from data source
			this.DataOutput.PropertyChanged -= this.OnDataOutputPropertyChanged;
		}


		/// <summary>
		/// Get unique ID of this <see cref="LogReader"/> instance.
		/// </summary>
		public int Id { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Get or set list of <see cref="Log"/> to be output.
		/// </summary>
		public IList<Log> Logs
		{
			get => this.logs;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				this.logs = value.IsNotEmpty() ? new List<Log>(value).AsReadOnly() : Array.Empty<Log>();
				this.OnPropertyChanged(nameof(Logs));
			}
		}


		// Property of data output changed.
		void OnDataOutputPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
			this.OnDataOutputPropertyChanged(e);


		/// <summary>
		/// Called when property of <see cref="DataOutput"/> changed.
		/// </summary>
		/// <param name="e">Event data.</param>
		protected virtual void OnDataOutputPropertyChanged(PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataOutput.State) && this.state == LogWriterState.Starting)
				this.StartWritingLogs();
		}


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Start writing logs.
		/// </summary>
		public void Start()
		{
			// check state
			this.VerifyAccess();
			switch (this.state)
			{
				case LogWriterState.Preparing:
					break;
				case LogWriterState.Starting:
				case LogWriterState.WritingLogs:
					return;
				default:
					throw new InvalidOperationException($"Cannot start writing log in state {this.state}.");
			}

			// change state
			if (!this.ChangeState(LogWriterState.Starting))
				throw new InternalStateCorruptedException();

			// start writing logs
			this.StartWritingLogs();
		}


		// Start writing logs.
		async void StartWritingLogs()
		{
			// check state
			if (this.state != LogWriterState.Starting)
				return;
			switch (this.DataOutput.State)
			{
				case LogDataOutputState.ReadyToOpenWriter:
					break;
				case LogDataOutputState.TargetNotFound:
				case LogDataOutputState.UnclassifiedError:
					this.Logger.LogError("Unable to start writing logs because of data output state is {state}", this.DataOutput.State);
					this.ChangeState(LogWriterState.DataOutputError);
					return;
				default:
					this.Logger.LogWarning("Wait for data output ready");
					return;
			}

			// open writer
			var writer = (TextWriter?)null;
			try
			{
				writer = await this.DataOutput.OpenWriterAsync();
			}
			catch (Exception ex)
			{
				if (this.state == LogWriterState.Starting)
				{
					this.Logger.LogError(ex, "Unable to open writer");
					this.ChangeState(LogWriterState.DataOutputError);
				}
				return;
			}

			// start writing
			var writingTask = (Task?)null;
			try
			{
				writingTask = this.WriteLogsAsync(writer, this.writingLogsCancellationTokenSource.Token);
			}
			catch (Exception ex)
			{
				Global.RunWithoutErrorAsync(writer.Dispose);
				if (this.state == LogWriterState.Starting)
				{
					this.Logger.LogError(ex, "Unable to start writing logs");
					this.ChangeState(LogWriterState.DataOutputError);
				}
				return;
			}
			if (!this.ChangeState(LogWriterState.WritingLogs))
			{
				Global.RunWithoutErrorAsync(writer.Dispose);
				return;
			}

			// waiting for writing logs
			var exception = (Exception?)null;
			try
			{
				await writingTask;
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Error occurred while writing logs");
				exception = ex;
			}
			finally
			{
				await Task.Run(() =>
				{
					Global.RunWithoutError(writer.Dispose);
				});
			}

			// complete writing
			if (this.state == LogWriterState.WritingLogs)
			{
				if (exception == null)
					this.ChangeState(LogWriterState.Stopped);
				else
					this.ChangeState(LogWriterState.UnclassifiedError);
			}
		}


		/// <summary>
		/// Get current state.
		/// </summary>
		public LogWriterState State { get => this.state; }


		/// <summary>
		/// Throw <see cref="InvalidOperationException"/> if current state if not <see cref="LogWriterState.Preparing"/>.
		/// </summary>
		protected void VerifyPreparing()
		{
			if (this.state != LogWriterState.Preparing)
				throw new InvalidOperationException($"Cannot perform operation when state is {this.state}.");
		}


		/// <summary>
		/// Called when write logs
		/// </summary>
		/// <param name="writer"><see cref="TextWriter"/> to write log data.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Task of writing logs.</returns>
		protected abstract Task WriteLogsAsync(TextWriter writer, CancellationToken cancellationToken);


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";
	}
}
