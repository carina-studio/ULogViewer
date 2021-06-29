using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Log profile.
	/// </summary>
	class LogProfile : IApplicationObject, INotifyPropertyChanged
	{
		/// <summary>
		/// Default comparer for <see cref="LogProfile"/>.
		/// </summary>
		public static readonly IComparer<LogProfile> Comparer = Comparer<LogProfile>.Create(Compare);


		// Fields.
		LogDataSourceOptions dataSourceOptions;
		ILogDataSourceProvider dataSourceProvider = LogDataSourceProviders.Empty;
		Dictionary<string, LogLevel> logLevelMap = new Dictionary<string, LogLevel>();
		IList<LogPattern> logPatterns = new LogPattern[0];
		string name = "";
		IDictionary<string, LogLevel> readOnlyLogLevelMap;
		SortDirection sortDirection = SortDirection.Ascending;
		string? timestampFormatForReading;


		/// <summary>
		/// Initialize new <see cref="LogProfile"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public LogProfile(IApplication app)
		{
			app.VerifyAccess();
			this.Application = app;
			this.readOnlyLogLevelMap = new ReadOnlyDictionary<string, LogLevel>(this.logLevelMap);
		}


		/// <summary>
		/// Initialize new <see cref="LogProfile"/> instance.
		/// </summary>
		/// <param name="template">Template profile.</param>
		public LogProfile(LogProfile template) : this(template.Application)
		{
			this.dataSourceOptions = template.dataSourceOptions;
			this.dataSourceProvider = template.dataSourceProvider;
			foreach (var pair in template.logLevelMap)
				this.logLevelMap[pair.Key] = pair.Value;
			this.logPatterns = template.logPatterns;
			this.name = template.name;
			this.sortDirection = template.sortDirection;
			this.timestampFormatForReading = template.timestampFormatForReading;
		}


		// Constructor for built-in profile.
		LogProfile(IApplication app, string builtInId) : this(app)
		{
			this.BuiltInId = builtInId;
			this.UpdateBuiltInName();
		}


		/// <summary>
		/// Get application instance.
		/// </summary>
		public IApplication Application { get; }


		// ID of built-in profile.
		string? BuiltInId { get; }


		// Compare profiles.
		static int Compare(LogProfile? x, LogProfile? y)
		{
			// check instance
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by name of data source provider
			var result = x.dataSourceProvider.Name.CompareTo(y.dataSourceProvider.Name);
			if (result != 0)
				return result;

			// compare by built-in state
			if (x.IsBuiltIn)
			{
				if (!y.IsBuiltIn)
					return -1;
			}
			else if (y.IsBuiltIn)
				return 1;

			// compare by name
			result = x.name.CompareTo(y.name);
			if (result != 0)
				return result;

			// compare by hash-code
			return (x.GetHashCode() - y.GetHashCode());
		}


		/// <summary>
		/// Get or set <see cref="LogDataSourceOptions"/> to create <see cref="ILogDataSource"/>.
		/// </summary>
		public LogDataSourceOptions DataSourceOptions
		{
			get => this.dataSourceOptions;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.dataSourceOptions == value)
					return;
				this.dataSourceOptions = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataSourceOptions)));
			}
		}


		/// <summary>
		/// Get or set <see cref="ILogDataSourceProvider"/> to build <see cref="ILogDataSource"/> instances for logs reading.
		/// </summary>
		public ILogDataSourceProvider DataSourceProvider
		{
			get => this.dataSourceProvider;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.dataSourceProvider == value)
					return;
				this.dataSourceProvider = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataSourceProvider)));
			}
		}


		/// <summary>
		/// Check whether instance represents a built-in profile or not.
		/// </summary>
		public bool IsBuiltIn { get => this.BuiltInId != null; }


		/// <summary>
		/// Load built-in profile asynchronously.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="id">ID of built-in profile.</param>
		/// <returns>Task of loading operation.</returns>
		public static async Task<LogProfile> LoadBuiltInProfileAsync(IApplication app, string id)
		{
			// load JSON document
			var jsonDocument = await Task.Run(() =>
			{
				using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CarinaStudio.ULogViewer.Logs.Profiles.BuiltIn.{id}.json") ?? throw new ArgumentException($"Cannot find built-in profile '{id}'.");
				return JsonDocument.Parse(stream);
			});

			// prepare profile
			var profile = new LogProfile(app, id);

			// load profile
			await Task.Run(() => profile.LoadFromJson(jsonDocument.RootElement));

			// complete
			return profile;
		}


		// Load ILogDataSourceProvider from JSON element.
		void LoadDataSourceFromJson(JsonElement dataSourceElement)
		{
			// find provider
			var providerName = dataSourceElement.GetProperty("Name").GetString() ?? throw new ArgumentException("No name of data source.");
			if (!LogDataSourceProviders.TryFindProviderByName(providerName, out var provider) || provider == null)
				throw new ArgumentException($"Cannot find data source '{providerName}'.");

			// get options
			var options = new LogDataSourceOptions();
			if (dataSourceElement.TryGetProperty("Command", out var jsonProperty))
				options.Command = jsonProperty.GetString();
			if (dataSourceElement.TryGetProperty("Encoding", out jsonProperty))
				options.Encoding = Encoding.GetEncoding(jsonProperty.GetString().AsNonNull());
			if (dataSourceElement.TryGetProperty("FileName", out jsonProperty))
				options.FileName = jsonProperty.GetString();
			if (dataSourceElement.TryGetProperty("WorkingDirectory", out jsonProperty))
				options.WorkingDirectory = jsonProperty.GetString();

			// complete
			this.dataSourceOptions = options;
			this.dataSourceProvider = provider;
		}


		// Load profile from JSON element.
		void LoadFromJson(JsonElement profileElement)
		{
			// data source
			if (profileElement.TryGetProperty("DataSource", out var jsonValue))
				this.LoadDataSourceFromJson(jsonValue);

			// log level map
			if (profileElement.TryGetProperty("LogLevelMap", out jsonValue))
				this.LoadLogLevelMapFromJson(jsonValue);

			// log patterns
			if (profileElement.TryGetProperty("LogPatterns", out jsonValue))
				this.LoadLogPatternsFromJson(jsonValue);

			// sort direction
			if (profileElement.TryGetProperty("SortDirection", out jsonValue))
				this.sortDirection = Enum.Parse<SortDirection>(jsonValue.GetString().AsNonNull());

			// timestamp format
			if (profileElement.TryGetProperty("TimestampFormatForReading", out jsonValue))
				this.timestampFormatForReading = jsonValue.GetString();
		}


		// Load log level map from JSON.
		void LoadLogLevelMapFromJson(JsonElement logLevelMapElement)
		{
			this.logLevelMap.Clear();
			foreach (var jsonProperty in logLevelMapElement.EnumerateObject())
			{
				var key = jsonProperty.Name;
				var logLevel = Enum.Parse<LogLevel>(jsonProperty.Value.GetString() ?? "");
				this.logLevelMap[key] = logLevel;
			}
		}


		// Load log patterns from JSON.
		void LoadLogPatternsFromJson(JsonElement logPatternsElement)
		{
			var logPatterns = new List<LogPattern>();
			foreach (var logPatternElement in logPatternsElement.EnumerateArray())
			{
				var regex = new Regex(logPatternElement.GetProperty("Regex").GetString().AsNonNull());
				var isRepeatable = false;
				var isSkippable = false;
				if (logPatternElement.TryGetProperty("IsRepeatable", out var jsonProperty))
					isRepeatable = jsonProperty.GetBoolean();
				if (logPatternElement.TryGetProperty("IsSkippable", out jsonProperty))
					isSkippable = jsonProperty.GetBoolean();
				logPatterns.Add(new LogPattern(regex, isRepeatable, isSkippable));
			}
			this.logPatterns = logPatterns.AsReadOnly();
		}


		/// <summary>
		/// Get or set map of conversion from string to <see cref="LogLevel"/>.
		/// </summary>
		public IDictionary<string, LogLevel> LogLevelMap
		{
			get => this.readOnlyLogLevelMap;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				this.logLevelMap.Clear();
				foreach (var pair in value)
					this.logLevelMap[pair.Key] = pair.Value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogLevelMap)));
			}
		}


		/// <summary>
		/// Get of set list of <see cref="LogPattern"/> to parse log data.
		/// </summary>
		public IList<LogPattern> LogPatterns
		{
			get => this.logPatterns;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.logPatterns.SequenceEqual(value))
					return;
				this.logPatterns = value.AsReadOnly();
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogPatterns)));
			}
		}


		/// <summary>
		/// Get of set name of profile.
		/// </summary>
		public string Name
		{
			get => this.name;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.name == value)
					return;
				this.name = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
			}
		}


		/// <summary>
		/// Called when application string resources updated.
		/// </summary>
		public void OnApplicationStringsUpdated() => this.UpdateBuiltInName();


		/// <summary>
		/// Raised when property changed.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;


		/// <summary>
		/// Get of set direction of log sorting.
		/// </summary>
		public SortDirection SortDirection
		{
			get => this.sortDirection;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.sortDirection == value)
					return;
				this.sortDirection = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortDirection)));
			}
		}


		/// <summary>
		/// Get or set format of timestamp for reading logs.
		/// </summary>
		public string? TimestampFormatForReading
		{
			get => this.timestampFormatForReading;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.timestampFormatForReading == value)
					return;
				this.timestampFormatForReading = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimestampFormatForReading)));
			}
		}


		// Update name of built-in profile.
		void UpdateBuiltInName()
		{
			if (this.BuiltInId == null)
				return;
			var name = this.Application.GetStringNonNull($"BuiltInProfile.{this.BuiltInId}", this.BuiltInId);
			if (this.name == name)
				return;
			this.name = name;
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
		}


		/// <summary>
		/// Check whether properties of profile is valid or not.
		/// </summary>
		/// <returns>True if properties of profile is valid.</returns>
		public bool Validate() => !(this.dataSourceProvider is EmptyLogDataSourceProvider)
				&& this.logPatterns.IsNotEmpty();


		// Throw exception if profile is built-in.
		void VerifyBuiltIn()
		{
			if (this.IsBuiltIn)
				throw new InvalidOperationException();
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
