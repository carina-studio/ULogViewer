using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
		// Fields.
		LogDataSourceOptions dataSourceOptions;
		ILogDataSourceProvider dataSourceProvider = LogDataSourceProviders.Empty;
		IList<LogPattern> logPatterns = new LogPattern[0];


		// Constructor for built-in profile.
		LogProfile(IApplication app, string builtInId)
		{
			app.VerifyAccess();
			this.Application = app;
			this.BuiltInId = builtInId;
		}


		/// <summary>
		/// Get application instance.
		/// </summary>
		public IApplication Application { get; }


		// ID of built-in profile.
		string? BuiltInId { get; }


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
			if (profileElement.TryGetProperty("DataSource", out var jsonProperty))
				this.LoadDataSourceFromJson(jsonProperty);

			// log patterns
			if (profileElement.TryGetProperty("LogPatterns", out jsonProperty))
				this.LoadLogPatternsFromJson(jsonProperty);
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
		/// Raised when property changed.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;


		/// <summary>
		/// Check whether properties of profile is valid or not.
		/// </summary>
		/// <returns>True if properties of profile is valid.</returns>
		public bool Validate() => !(this.dataSourceProvider is EmptyLogDataSourceProvider)
				&& this.logPatterns.IsNotEmpty();


		/// <summary>
		/// Get or set format of timestamp for reading logs.
		/// </summary>
		public string? TimestampFormatForReading { get; set; }


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
