using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Log reader.
	/// </summary>
	class LogReader : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Fields.
		bool isContinuousReading;
		IList<LogPattern> logPatterns = new LogPattern[0];
		readonly ObservableCollection<Log> logs = new ObservableCollection<Log>();
		LogReaderState state = LogReaderState.Preparing;


		/// <summary>
		/// Initialize new <see cref="LogReader"/> instance.
		/// </summary>
		/// <param name="dataSource"><see cref="ILogDataSource"/> to read log data from.</param>
		/// <param name="logPatterns">Log patterns to parse log data.</param>
		public LogReader(ILogDataSource dataSource, IEnumerable<LogPattern> logPatterns)
		{
			// check thread
			dataSource.VerifyAccess();

			// setup properties
			this.Application = (IApplication)dataSource.Application;
			this.DataSource = dataSource;
			this.Logs = new ReadOnlyObservableCollection<Log>(this.logs);

			// attach to data source
			dataSource.PropertyChanged += this.OnDataSourcePropertyChanged;
		}


		/// <summary>
		/// Get <see cref="IApplication"/> instance.
		/// </summary>
		public IApplication Application { get; }


		// Change state.
		bool ChangeState(LogReaderState state)
		{
			if (this.state == state)
				return true;
			this.state = state;
			this.OnPropertyChanged(nameof(State));
			return (this.state == state);
		}


		/// <summary>
		/// Get <see cref="ILogDataSource"/> to read log data from.
		/// </summary>
		public ILogDataSource DataSource { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check state.
			if (disposing)
				this.VerifyAccess();
			else
				return; // ignore releasing managed resources

			// change state
			this.ChangeState(LogReaderState.Disposed);

			// detach from data source
			this.DataSource.PropertyChanged -= this.OnDataSourcePropertyChanged;

			// clear logs
			this.logs.Clear();
		}


		/// <summary>
		/// Get or set whether restart reading is needed when reaching end of log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public bool IsContinuousReading
		{
			get => this.isContinuousReading;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(IsContinuousReading)} when state is {this.state}.");
				if (this.isContinuousReading == value)
					return;
				this.isContinuousReading = value;
				this.OnPropertyChanged(nameof(IsContinuousReading));
			}
		}


		/// <summary>
		/// Get or set log patterns to parse log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public IList<LogPattern> LogPatterns 
		{
			get => this.logPatterns;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogPatterns)} when state is {this.state}.");
				if (this.logPatterns.SequenceEqual(value))
					return;
				this.logPatterns = value.ToList().AsReadOnly();
				this.OnPropertyChanged(nameof(LogPatterns));
			}
		}


		/// <summary>
		/// Get list of read <see cref="Log"/>s.
		/// </summary>
		/// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
		public IList<Log> Logs { get; }


		// Called when property of data source has been changed.
		void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => this.OnDataSourcePropertyChanged(e);


		/// <summary>
		/// Called when property of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="e">Event data.</param>
		protected virtual void OnDataSourcePropertyChanged(PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataSource.State))
				this.OnDataSourceStateChanged(this.DataSource.State);
		}


		/// <summary>
		/// Called when state of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="state">New state.</param>
		protected virtual void OnDataSourceStateChanged(LogDataSourceState state)
		{ }


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Get current state of <see cref="LogReader"/>.
		/// </summary>
		public LogReaderState State { get => this.state; }


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}


	/// <summary>
	/// State of <see cref="LogReader"/>.
	/// </summary>
	enum LogReaderState
	{
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Starting to read logs.
		/// </summary>
		Starting,
		/// <summary>
		/// Reading logs.
		/// </summary>
		ReadingLogs,
		/// <summary>
		/// Stopping from reading logs.
		/// </summary>
		Stopping,
		/// <summary>
		/// Stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// Error caused by <see cref="LogReader.DataSource"/>.
		/// </summary>
		DataSourceError,
		/// <summary>
		/// Unclassified error.
		/// </summary>
		UnclassifiedError,
		/// <summary>
		/// Disposed.
		/// </summary>
		Disposed,
	}
}
