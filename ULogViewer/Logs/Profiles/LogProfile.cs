using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
		bool isPinned;
		SettingKey<bool>? isPinnedSettingKey;
		bool isWorkingDirectoryNeeded;
		readonly ILogger logger;
		Dictionary<string, LogLevel> logLevelMap = new Dictionary<string, LogLevel>();
		IList<LogPattern> logPatterns = new LogPattern[0];
		string name = "";
		IDictionary<string, LogLevel> readOnlyLogLevelMap;
		SortDirection sortDirection = SortDirection.Ascending;
		CultureInfo? timestampCultureInfoForReading;
		string? timestampFormatForDisplaying;
		string? timestampFormatForReading;
		IList<LogProperty> visibleLogProperties = new LogProperty[0];


		/// <summary>
		/// Initialize new <see cref="LogProfile"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public LogProfile(IApplication app)
		{
			app.VerifyAccess();
			this.Application = app;
			this.logger = app.LoggerFactory.CreateLogger(this.GetType().Name);
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
			this.isPinnedSettingKey = new SettingKey<bool>($"BuiltInProfile.{builtInId}.IsPinned");
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
				this.Validate();
			}
		}


		/// <summary>
		/// Check whether instance represents a built-in profile or not.
		/// </summary>
		public bool IsBuiltIn { get => this.BuiltInId != null; }


		/// <summary>
		/// Get or set whether profile should be pinned at quick access area or not.
		/// </summary>
		public bool IsPinned
		{
			get => this.isPinned;
			set
			{
				this.VerifyAccess();
				if (this.isPinned == value)
					return;
				this.isPinned = value;
				if (this.IsBuiltIn)
					this.Application.Settings.SetValue(this.isPinnedSettingKey.AsNonNull(), value);
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
			}
		}


		/// <summary>
		/// Check whether properties of profile is valid or not.
		/// </summary>
		public bool IsValid { get; private set; }


		/// <summary>
		/// Get or set whether working directory is needed to be set before reading logs or not.
		/// </summary>
		public bool IsWorkingDirectoryNeeded
		{
			get => this.isWorkingDirectoryNeeded;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.isWorkingDirectoryNeeded == value)
					return;
				this.isWorkingDirectoryNeeded = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWorkingDirectoryNeeded)));
			}
		}


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
			profile.isPinned = app.Settings.GetValueOrDefault(profile.isPinnedSettingKey.AsNonNull());

			// validate
			profile.Validate();

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
			foreach (var jsonProperty in dataSourceElement.EnumerateObject())
			{
				switch (jsonProperty.Name)
				{
					case nameof(LogDataSourceOptions.Command):
						options.Command = jsonProperty.Value.GetString();
						break;
					case nameof(LogDataSourceOptions.Encoding):
						options.Encoding = Encoding.GetEncoding(jsonProperty.Value.GetString().AsNonNull());
						break;
					case nameof(LogDataSourceOptions.FileName):
						options.FileName = jsonProperty.Value.GetString();
						break;
					case "Name":
						break;
					case nameof(LogDataSourceOptions.WorkingDirectory):
						options.WorkingDirectory = jsonProperty.Value.GetString();
						break;
					default:
						this.logger.LogWarning($"Unknown property of DataSource: {jsonProperty.Name}");
						break;
				}
			}

			// complete
			this.dataSourceOptions = options;
			this.dataSourceProvider = provider;
		}


		// Load profile from JSON element.
		void LoadFromJson(JsonElement profileElement)
		{
			foreach (var jsonProperty in profileElement.EnumerateObject())
			{
				switch (jsonProperty.Name)
				{
					case "DataSource":
						this.LoadDataSourceFromJson(jsonProperty.Value);
						break;
					case "IsWorkingDirectoryNeeded":
						this.isWorkingDirectoryNeeded = jsonProperty.Value.GetBoolean();
						break;
					case "LogLevelMap":
						this.LoadLogLevelMapFromJson(jsonProperty.Value);
						break;
					case "LogPatterns":
						this.LoadLogPatternsFromJson(jsonProperty.Value);
						break;
					case "SortDirection":
						this.sortDirection = Enum.Parse<SortDirection>(jsonProperty.Value.GetString().AsNonNull());
						break;
					case "TimestampCultureInfoForReading":
						this.timestampCultureInfoForReading = CultureInfo.GetCultureInfo(jsonProperty.Value.GetString().AsNonNull());
						break;
					case "TimestampFormatForDisplaying":
						this.timestampFormatForDisplaying = jsonProperty.Value.GetString();
						break;
					case "TimestampFormatForReading":
						this.timestampFormatForReading = jsonProperty.Value.GetString();
						break;
					case "VisibleLogProperties":
						this.LoadVisibleLogPropertiesFromJson(jsonProperty.Value);
						break;
					default:
						this.logger.LogWarning($"Unknown property of profile: {jsonProperty.Name}");
						break;
				}
			}
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


		// Load visible log properties from JSON.
		void LoadVisibleLogPropertiesFromJson(JsonElement visibleLogPropertiesElement)
		{
			var logProperties = new List<LogProperty>();
			foreach (var logPropertyElement in visibleLogPropertiesElement.EnumerateArray())
			{
				var name = logPropertyElement.GetProperty("Name").GetString().AsNonNull();
				var displayName = (string?)null;
				var width = (int?)null;
				if (logPropertyElement.TryGetProperty("DisplayName", out var jsonElement))
					displayName = jsonElement.GetString();
				if (logPropertyElement.TryGetProperty("Width", out jsonElement))
					width = jsonElement.GetInt32();
				logProperties.Add(new LogProperty(name, displayName, width));
			}
			this.visibleLogProperties = logProperties.AsReadOnly();
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
				this.Validate();
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
		/// Get or set <see cref="CultureInfo"/> of timestamp for reading logs.
		/// </summary>
		public CultureInfo? TimestampCultureInfoForReading
		{
			get => this.timestampCultureInfoForReading;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.timestampCultureInfoForReading?.Equals(value) ?? value == null)
					return;
				this.timestampCultureInfoForReading = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimestampCultureInfoForReading)));
			}
		}


		/// <summary>
		/// Get or set format of timestamp for displaying logs.
		/// </summary>
		public string? TimestampFormatForDisplaying
		{
			get => this.timestampFormatForDisplaying;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.timestampFormatForDisplaying == value)
					return;
				this.timestampFormatForDisplaying = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimestampFormatForDisplaying)));
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


		// Check whether properties of profile is valid or not.
		void Validate()
		{
			var isValid = this.dataSourceProvider is not EmptyLogDataSourceProvider
				&& this.logPatterns.IsNotEmpty()
				&& this.visibleLogProperties.Let(it =>
				{
					if (it.IsEmpty())
						return false;
					foreach (var property in it)
					{
						if (!Log.HasProperty(property.Name))
							return false;
					}
					return true;
				});
			if (this.IsValid == isValid)
				return;
			this.IsValid = isValid;
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
		}


		// Throw exception if profile is built-in.
		void VerifyBuiltIn()
		{
			if (this.IsBuiltIn)
				throw new InvalidOperationException();
		}


		// <summary>
		/// Get of set list of log properties to be shown to user.
		/// </summary>
		public IList<LogProperty> VisibleLogProperties
		{
			get => this.visibleLogProperties;
			set
			{
				this.VerifyAccess();
				this.VerifyBuiltIn();
				if (this.visibleLogProperties.SequenceEqual(value))
					return;
				this.visibleLogProperties = value.AsReadOnly();
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibleLogProperties)));
				this.Validate();
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
