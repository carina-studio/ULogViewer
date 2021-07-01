using CarinaStudio.Collections;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Session to view logs.
	/// </summary>
	class Session : ViewModel
	{
		/// <summary>
		/// Property of <see cref="LogProfile"/>.
		/// </summary>
		public static readonly ObservableProperty<LogProfile?> LogProfileProperty = ObservableProperty.Register<Session, LogProfile?>(nameof(LogProfile));
		/// <summary>
		/// Property of <see cref="Logs"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<Log>> LogsProperty = ObservableProperty.Register<Session, IList<Log>>(nameof(Logs), new Log[0]);


		// Fields.
		readonly SortedObservableList<Log> allLogs;
		readonly MutableObservableBoolean canOpenFile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canResetLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSelectLogProfile = new MutableObservableBoolean();
		IComparer<Log> logComparer = LogComparers.TimestampAsc;
		readonly HashSet<LogReader> logReaders = new HashSet<LogReader>();
		readonly HashSet<string> openedFilePaths = new HashSet<string>(PathEqualityComparer.Default);


		/// <summary>
		/// Initialize new <see cref="Session"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public Session(IApplication app) : base(app)
		{
			// create commands
			this.OpenLogFileCommand = ReactiveCommand.Create<string?>(this.OpenLogFile, this.canOpenFile);
			this.ResetLogProfileCommand = ReactiveCommand.Create(this.ResetLogProfile, this.canResetLogProfile);
			this.SelectLogProfileCommand = ReactiveCommand.Create<LogProfile?>(this.SelectLogProfile, this.canSelectLogProfile);
			this.canSelectLogProfile.Update(true);

			// create collections
			this.allLogs = new SortedObservableList<Log>(this.CompareLogs).Also(it =>
			{
				it.CollectionChanged += this.OnAllLogsChanged;
			});

			// setup properties
			this.SetValue(LogsProperty, this.allLogs.AsReadOnly());
		}


		// Compare logs.
		int CompareLogs(Log? x, Log? y) => this.logComparer.Compare(x, y);


		// Try creating log data source.
		ILogDataSource? CreateLogDataSourceOrNull(ILogDataSourceProvider provider, LogDataSourceOptions options)
		{
			try
			{
				return provider.CreateSource(options);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to create data source");
				return null;
			}
		}


		// Create log reader.
		void CreateLogReader(ILogDataSource dataSource)
		{
			// create log reader
			var logReader = new LogReader(dataSource);
			this.logReaders.Add(logReader);
			this.Logger.LogDebug($"Log reader '{logReader.Id} created");

			// add event handlers
			dataSource.PropertyChanged += this.OnLogDataSourcePropertyChanged;
			logReader.LogsChanged += this.OnLogReaderLogsChanged;
			logReader.PropertyChanged += this.OnLogReaderPropertyChanged;

			// add logs
			this.allLogs.AddAll(logReader.Logs);
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// ignore managed resources
			if (!disposing)
			{
				base.Dispose(disposing);
				return;
			}

			// check thread
			this.VerifyAccess();

			// cancel filtering
			//

			// dispose log readers
			this.DisposeLogReaders(false);

			// clear logs
			this.allLogs.Clear();

			// call base
			base.Dispose(disposing);
		}


		// Dispose log reader.
		void DisposeLogReader(LogReader logReader, bool removeLogs)
		{
			// remove from set
			if (!this.logReaders.Remove(logReader))
				return;

			// remove event handlers
			var dataSource = logReader.DataSource;
			dataSource.PropertyChanged -= this.OnLogDataSourcePropertyChanged;
			logReader.LogsChanged -= this.OnLogReaderLogsChanged;
			logReader.PropertyChanged -= this.OnLogReaderPropertyChanged;

			// remove logs
			if (removeLogs)
				this.allLogs.RemoveAll(it => it.Reader == logReader);

			// dispose data source and log reader
			logReader.Dispose();
			dataSource.Dispose();
			this.Logger.LogDebug($"Log reader '{logReader.Id} disposed");
		}


		// Dispose all log readers.
		void DisposeLogReaders(bool removeLogs)
		{
			foreach (var logReader in this.logReaders.ToArray())
				this.DisposeLogReader(logReader, removeLogs);
		}


		/// <summary>
		/// Get current log profile.
		/// </summary>
		public LogProfile? LogProfile { get => this.GetValue(LogProfileProperty); }


		/// <summary>
		/// Get list of <see cref="Log"/>s to display.
		/// </summary>
		public IList<Log> Logs { get => this.GetValue(LogsProperty); }


		// Called when logs in allLogs has been changed.
		void OnAllLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (this.LogProfile == null || this.IsDisposed)
				return;
		}


		// Called when property of log data source changed.
		void OnLogDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{ }


		// Called when logs of log reader has been changed.
		void OnLogReaderLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			var logReader = (LogReader)sender.AsNonNull();
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					this.allLogs.AddAll((IEnumerable<Log>)e.NewItems.AsNonNull());
					break;
				case NotifyCollectionChangedAction.Remove:
					this.allLogs.RemoveAll((IEnumerable<Log>)e.OldItems.AsNonNull());
					break;
				case NotifyCollectionChangedAction.Reset:
					this.allLogs.RemoveAll(it => it.Reader == logReader);
					break;
				default:
					throw new InvalidOperationException($"Unsupported logs change action: {e.Action}.");
			}
		}


		// Called when property of log reader changed.
		void OnLogReaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{ }


		// Open file.
		void OpenLogFile(string? fileName)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canOpenFile.Value)
				return;
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to open file.");
			if (profile.DataSourceProvider.UnderlyingSource != UnderlyingLogDataSource.File)
				throw new InternalStateCorruptedException($"Cannot open file because underlying data source type is {profile.DataSourceProvider.UnderlyingSource}.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.FileName != null)
				throw new InternalStateCorruptedException($"Cannot open file because file name is already specified.");
			if (!this.openedFilePaths.Add(fileName))
			{
				this.Logger.LogWarning($"File '{fileName}' is already opened");
				return;
			}

			// create data source
			dataSourceOptions.FileName = fileName;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
				return;

			// create log reader
			this.CreateLogReader(dataSource);
		}


		/// <summary>
		/// Command to open log file.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="string"/>.</remarks>
		public ICommand OpenLogFileCommand { get; }


		// Reset log profile.
		void ResetLogProfile()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canResetLogProfile.Value)
				return;
			var profile = this.LogProfile;
			if (profile == null)
				throw new InternalStateCorruptedException("No log profile to reset.");

			// clear profile
			this.Logger.LogWarning($"Reset log profile '{profile.Name}'");
			this.SetValue(LogProfileProperty, null);

			// cancel filtering
			//

			// dispose log readers
			this.DisposeLogReaders(false);

			// clear logs
			this.allLogs.Clear();

			// clear file name table
			this.openedFilePaths.Clear();

			// update state
			this.canOpenFile.Update(false);
			this.canResetLogProfile.Update(false);
			this.canSelectLogProfile.Update(true);
		}


		/// <summary>
		/// Command to reset log profile.
		/// </summary>
		public ICommand ResetLogProfileCommand { get; }


		// Select log profile.
		void SelectLogProfile(LogProfile? profile)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canSelectLogProfile.Value)
				return;
			if (profile == null)
				throw new ArgumentNullException(nameof(profile));
			if (this.LogProfile != null)
				throw new InternalStateCorruptedException("Already select another log profile.");

			// select profile
			this.Logger.LogWarning($"Select profile '{profile.Name}'");
			this.canSelectLogProfile.Update(false);
			this.SetValue(LogProfileProperty, profile);

			// read logs or wait for more actions
			var dataSourceOptions = profile.DataSourceOptions;
			var dataSourceProvider = profile.DataSourceProvider;
			switch (dataSourceProvider.UnderlyingSource)
			{
				case UnderlyingLogDataSource.File:
					if (dataSourceOptions.FileName == null)
					{
						this.Logger.LogDebug("No file name specified, waiting for opening file");
						this.canOpenFile.Update(true);
					}
					else
					{
						this.Logger.LogDebug("File name specified, start reading logs");
						var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, dataSourceOptions);
						if (dataSource != null)
							this.CreateLogReader(dataSource);
					}
					break;
				case UnderlyingLogDataSource.StandardOutput:
					if (dataSourceOptions.Command == null)
						this.Logger.LogError("No command to open standard output");
					else
					{
						this.Logger.LogDebug("Start reading logs from standard output");
						var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, dataSourceOptions);
						if (dataSource != null)
							this.CreateLogReader(dataSource);
					}
					break;
				default:
					this.Logger.LogError($"Unsupported underlying log data source type: {dataSourceProvider.UnderlyingSource}");
					break;
			}

			// update state
			this.canResetLogProfile.Update(true);
		}


		/// <summary>
		/// Command to select specific log profile.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="LogProfile"/>.</remarks>
		public ICommand SelectLogProfileCommand { get; }
	}
}
