using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Cryptography;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Provider of <see cref="ILogDataSource"/>.
	/// </summary>
	interface ILogDataSourceProvider : IApplicationObject, INotifyPropertyChanged, IThreadDependent
	{
		/// <summary>
		/// Get number of active <see cref="ILogDataSource"/> instances created by this provider.
		/// </summary>
		int ActiveSourceCount { get; }


		/// <summary>
		/// Check whether multiple active <see cref="ILogDataSource"/> instances created by this provider is allowed or not.
		/// </summary>
		bool AllowMultipleSources { get; }


		/// <summary>
		/// Create <see cref="ILogDataSource"/> instance.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns><see cref="ILogDataSource"/> instance.</returns>
		ILogDataSource CreateSource(LogDataSourceOptions options);


		/// <summary>
		/// Get name for displaying purpose.
		/// </summary>
		string DisplayName { get; }


		/// <summary>
		/// Get all external dependencies needed by the source.
		/// </summary>
		IEnumerable<ExternalDependency> ExternalDependencies { get; }


		/// <summary>
		/// Get URI of reference of specific option in <see cref="LogDataSourceOptions"/>.
		/// </summary>
		/// <param name="name">Name of specific option.</param>
		/// <returns>URI of reference.</returns>
		Uri? GetSourceOptionReferenceUri(string name);


		/// <summary>
		/// Check whether the provider can be used in Pro version only or not.
		/// </summary>
		bool IsProVersionOnly { get; }


		/// <summary>
		/// Get unique name to identify this provider.
		/// </summary>
		string Name { get; }


		/// <summary>
		/// Get the set of name of options which are required by creating <see cref="ILogDataSource"/>.
		/// </summary>
		ISet<string> RequiredSourceOptions { get; }


		/// <summary>
		/// Get the set of name of options which are supported by this provider and created <see cref="ILogDataSource"/>.
		/// </summary>
		ISet<string> SupportedSourceOptions { get; }


		/// <summary>
		/// Validate whether given options are valid for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="options">Options to check.</param>
		/// <returns>True if options are valid for creating <see cref="ILogDataSource"/>.</returns>
		bool ValidateSourceOptions(LogDataSourceOptions options);
	}


	/// <summary>
	/// Options to create <see cref="ILogDataSource"/>.
	/// </summary>
	public struct LogDataSourceOptions
	{
		// Static fields.
		static readonly IList<string> emptyCommands = Array.Empty<string>();
		static volatile bool isOptionPropertyInfoMapReady;
		static volatile IList<string> optionNames = Array.Empty<string>();
		static readonly Dictionary<string, PropertyInfo> optionPropertyInfoMap = new();


		// Fields.
		IList<string>? setupCommands;
		IList<string>? teardownCommands;


		/// <summary>
		/// Get or set category to read log data.
		/// </summary>
		public string? Category { get; set; }


		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		public string? Command { get; set; }


		/// <summary>
		/// Get or set connection string to connect database.
		/// </summary>
		public string? ConnectionString { get; set; }


		/// <summary>
		/// Get or set encoding of text.
		/// </summary>
		public Encoding? Encoding { get; set; }


		// Check equality.
		public override bool Equals(object? obj)
		{
			if (obj is LogDataSourceOptions options)
			{
				return this.Category == options.Category
					&& this.Command == options.Command
					&& this.ConnectionString == options.ConnectionString
					&& this.Encoding == options.Encoding
					&& this.FileName == options.FileName
					&& this.FormatJsonData == options.FormatJsonData
					&& this.FormatXmlData == options.FormatXmlData
					&& this.IncludeStandardError == options.IncludeStandardError
					&& object.Equals(this.IPEndPoint, options.IPEndPoint)
					&& this.IsResourceOnAzure == options.IsResourceOnAzure
					&& this.Password == options.Password
					&& this.QueryString == options.QueryString
					&& this.SetupCommands.SequenceEqual(options.SetupCommands)
					&& this.TeardownCommands.SequenceEqual(options.TeardownCommands)
					&& this.UseTextShell == options.UseTextShell
					&& this.Uri == options.Uri
					&& this.UserName == options.UserName
					&& this.WorkingDirectory == options.WorkingDirectory;
			}
			return false;
		}


		/// <summary>
		/// Get or set name of file to open.
		/// </summary>
		public string? FileName { get; set; }


		/// <summary>
		/// Get or set whether JSON data should be formatted when reading or not.
		/// </summary>
		public bool FormatJsonData { get; set; }


		/// <summary>
		/// Get or set whether XML data should be formatted when reading or not.
		/// </summary>
		public bool FormatXmlData { get; set; }


		// Get hash code.
		public override int GetHashCode()
		{
			if (this.Category != null)
				return this.Category.GetHashCode();
			if (this.Command != null)
				return this.Command.GetHashCode();
			if (this.ConnectionString != null)
				return this.ConnectionString.GetHashCode();
			if (this.FileName != null)
				return this.FileName.GetHashCode();
			if (this.IPEndPoint != null)
				return this.IPEndPoint.GetHashCode();
			if (this.Uri != null)
				return this.Uri.GetHashCode();
			return 0;
		}


		/// <summary>
		/// Get specific option.
		/// </summary>
		/// <param name="optionName">Name of option to get.</param>
		/// <returns>Value of option.</returns>
		public object? GetOption(string optionName)
		{
			SetupOptionPropertyInfoMap();
			if (optionPropertyInfoMap.TryGetValue(optionName, out var propertyInfo))
				return propertyInfo?.GetValue(this);
			return null;
		}


		/// <summary>
		/// Get or set whether standard errpr (stderr) should be included or not.
		/// </summary>
		public bool IncludeStandardError { get; set; }


		/// <summary>
		/// Get or set IP end point.
		/// </summary>
		public IPEndPoint? IPEndPoint { get; set; }


		/// <summary>
		/// Check whether given option has been set with value or not.
		/// </summary>
		/// <param name="optionName">Name of option.</param>
		/// <returns>True if option has been set with value.</returns>
		public bool IsOptionSet(string optionName)
		{
			SetupOptionPropertyInfoMap();
			if (!optionPropertyInfoMap.TryGetValue(optionName, out var propertyInfo) || propertyInfo == null)
				return false;
			var type = propertyInfo.PropertyType;
			if (type == typeof(string))
				return !string.IsNullOrWhiteSpace(propertyInfo.GetValue(this) as string);
			if (type == typeof(IList<string>))
				return (propertyInfo.GetValue(this) as IList<string>)?.Count > 0;
			return propertyInfo.GetValue(this) != null;
		}


		/// <summary>
		/// Check whether type of given option is <see cref="ValueType"/> or not.
		/// </summary>
		/// <param name="optionName">Name of option.</param>
		/// <returns>True if type of given option is <see cref="ValueType"/>.</returns>
		public static bool IsValueTypeOption(string optionName)
		{
			SetupOptionPropertyInfoMap();
			return optionPropertyInfoMap.TryGetValue(optionName, out var propertyInfo)
				&& propertyInfo.PropertyType.IsValueType;
		}


		/// <summary>
		/// Get or set whether the source is one of resource on Azure or not.
		/// </summary>
		public bool IsResourceOnAzure { get; set; }


		/// <summary>
		/// Equality operator.
		/// </summary>
		public static bool operator ==(LogDataSourceOptions x, LogDataSourceOptions y) => x.Equals(y);


		/// <summary>
		/// Inequality operator.
		/// </summary>
		public static bool operator !=(LogDataSourceOptions x, LogDataSourceOptions y) => !x.Equals(y);


		/// <summary>
		/// Load <see cref="LogDataSourceOptions"/> from object in JSON document.
		/// </summary>
		/// <param name="jsonObject">JSON object.</param>
		/// <returns><see cref="LogDataSourceOptions"/>.</returns>
		public static LogDataSourceOptions Load(JsonElement jsonObject)
		{
			var crypto = (Crypto?)null;
			try
			{
				if (jsonObject.ValueKind != JsonValueKind.Object)
					throw new JsonException("Given JSON element is not an object.");
				var options = new LogDataSourceOptions();
				foreach (var jsonProperty in jsonObject.EnumerateObject())
				{
					switch (jsonProperty.Name)
					{
						case nameof(Category):
							options.Category = jsonProperty.Value.GetString();
							break;
						case nameof(Command):
							options.Command = jsonProperty.Value.GetString();
							break;
						case nameof(ConnectionString):
							options.ConnectionString = jsonProperty.Value.GetString();
							break;
						case nameof(Encoding):
							options.Encoding = Encoding.GetEncoding(jsonProperty.Value.GetString().AsNonNull());
							break;
						case nameof(FileName):
							options.FileName = jsonProperty.Value.GetString();
							break;
						case nameof(FormatJsonData):
							options.FormatJsonData = jsonProperty.Value.ValueKind == JsonValueKind.True;
							break;
						case nameof(FormatXmlData):
							options.FormatXmlData = jsonProperty.Value.ValueKind == JsonValueKind.True;
							break;
						case nameof(IncludeStandardError):
							options.IncludeStandardError = jsonProperty.Value.ValueKind == JsonValueKind.True;
							break;
						case nameof(IPEndPoint):
							if (jsonProperty.Value.ValueKind == JsonValueKind.Object)
							{
								var jsonIPEndPoint = jsonProperty.Value;
								var address = IPAddress.Parse(jsonIPEndPoint.GetProperty(nameof(System.Net.IPEndPoint.Address)).GetString().AsNonNull());
								var port = jsonIPEndPoint.GetProperty(nameof(System.Net.IPEndPoint.Port)).GetInt32();
								options.IPEndPoint = new IPEndPoint(address, port);
							}
							else
								throw new JsonException($"JSON element of {nameof(IPEndPoint)} is not an object.");
							break;
						case nameof(IsResourceOnAzure):
							options.IsResourceOnAzure = jsonProperty.Value.GetBoolean();
							break;
						case nameof(Password):
							if (crypto == null)
								crypto = new Crypto(App.Current);
							options.Password = crypto.Decrypt(jsonProperty.Value.GetString().AsNonNull());
							break;
						case nameof(QueryString):
							options.QueryString = jsonProperty.Value.GetString();
							break;
						case nameof(SetupCommands):
							if (jsonProperty.Value.ValueKind == JsonValueKind.Array)
							{
								var commands = new List<string>();
								foreach (var jsonValue in jsonProperty.Value.EnumerateArray())
									commands.Add(jsonValue.GetString().AsNonNull());
								options.setupCommands = commands.AsReadOnly();
							}
							else
								throw new JsonException($"JSON element of {nameof(SetupCommands)} is not an array.");
							break;
						case nameof(TeardownCommands):
							if (jsonProperty.Value.ValueKind == JsonValueKind.Array)
							{
								var commands = new List<string>();
								foreach (var jsonValue in jsonProperty.Value.EnumerateArray())
									commands.Add(jsonValue.GetString().AsNonNull());
								options.teardownCommands = commands.AsReadOnly();
							}
							else
								throw new JsonException($"JSON element of {nameof(TeardownCommands)} is not an array.");
							break;
						case nameof(Uri):
							options.Uri = new Uri(jsonProperty.Value.GetString().AsNonNull());
							break;
						case nameof(UserName):
							if (crypto == null)
								crypto = new Crypto(App.Current);
							options.UserName = crypto.Decrypt(jsonProperty.Value.GetString().AsNonNull());
							break;
						case nameof(UseTextShell):
							options.UseTextShell = jsonProperty.Value.ValueKind == JsonValueKind.True;
							break;
						case nameof(WorkingDirectory):
							options.WorkingDirectory = jsonProperty.Value.GetString();
							break;
					}
				}
				return options;
			}
			finally
			{
				crypto?.Dispose();
			}
		}


		/// <summary>
		/// Get all names of options.
		/// </summary>
		public static IList<string> OptionNames
		{
			get
			{
				SetupOptionPropertyInfoMap();
				return optionNames;
			}
		}


		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		public string? Password { get; set; }


		/// <summary>
		/// Get or set query string.
		/// </summary>
		public string? QueryString { get; set; }


		/// <summary>
		/// Save options as JSON data.
		/// </summary>
		/// <param name="jsonWriter"><see cref="Utf8JsonWriter"/> to write JSON data.</param>
		public void Save(Utf8JsonWriter jsonWriter)
		{
			var crypto = (Crypto?)null;
			try
			{
				jsonWriter.WriteStartObject();
				this.Category?.Let(it => jsonWriter.WriteString(nameof(Category), it));
				this.Command?.Let(it => jsonWriter.WriteString(nameof(Command), it));
				this.ConnectionString?.Let(it => jsonWriter.WriteString(nameof(ConnectionString), it));
				this.Encoding?.Let(it => jsonWriter.WriteString(nameof(Encoding), it.WebName));
				this.FileName?.Let(it => jsonWriter.WriteString(nameof(FileName), it));
				if (this.FormatJsonData)
					jsonWriter.WriteBoolean(nameof(FormatJsonData), true);
				if (this.FormatXmlData)
					jsonWriter.WriteBoolean(nameof(FormatXmlData), true);
				if (this.IncludeStandardError)
					jsonWriter.WriteBoolean(nameof(IncludeStandardError), true);
				this.IPEndPoint?.Let(it =>
				{
					jsonWriter.WritePropertyName(nameof(IPEndPoint));
					jsonWriter.WriteStartObject();
					jsonWriter.WriteString(nameof(System.Net.IPEndPoint.Address), it.Address.ToString());
					jsonWriter.WriteNumber(nameof(System.Net.IPEndPoint.Port), it.Port);
					jsonWriter.WriteEndObject();
				});
				if (this.IsResourceOnAzure)
					jsonWriter.WriteBoolean(nameof(IsResourceOnAzure), true);
				this.Password?.Let(it =>
				{
					crypto = new Crypto(App.Current);
					jsonWriter.WriteString(nameof(Password), crypto.Encrypt(it));
				});
				this.QueryString?.Let(it => jsonWriter.WriteString(nameof(QueryString), it));
				this.setupCommands?.Let(it =>
				{
					if (it.IsNotEmpty())
					{
						jsonWriter.WritePropertyName(nameof(SetupCommands));
						jsonWriter.WriteStartArray();
						foreach (var command in it)
							jsonWriter.WriteStringValue(command);
						jsonWriter.WriteEndArray();
					}
				});
				this.teardownCommands?.Let(it =>
				{
					if (it.IsNotEmpty())
					{
						jsonWriter.WritePropertyName(nameof(TeardownCommands));
						jsonWriter.WriteStartArray();
						foreach (var command in it)
							jsonWriter.WriteStringValue(command);
						jsonWriter.WriteEndArray();
					}
				});
				this.Uri?.Let(it => jsonWriter.WriteString(nameof(Uri), it.ToString()));
				this.UserName?.Let(it =>
				{
					if (crypto == null)
						crypto = new Crypto(App.Current);
					jsonWriter.WriteString(nameof(UserName), crypto.Encrypt(it));
				});
				if (this.UseTextShell)
					jsonWriter.WriteBoolean(nameof(UseTextShell), true);
				this.WorkingDirectory?.Let(it => jsonWriter.WriteString(nameof(WorkingDirectory), it));
				jsonWriter.WriteEndObject();
			}
			finally
			{
				crypto?.Dispose();
			}
		}


		/// <summary>
		/// Get or set commands before executing <see cref="Command"/>.
		/// </summary>
		public IList<string> SetupCommands
		{
			get => this.setupCommands ?? emptyCommands;
			set => this.setupCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		// Setup table of property info of options.
		static void SetupOptionPropertyInfoMap()
		{
			if (isOptionPropertyInfoMapReady)
				return;
			lock (typeof(LogDataSourceOptions))
			{
				if (isOptionPropertyInfoMapReady)
					return;
				foreach (var propertyInfo in typeof(LogDataSourceOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
					optionPropertyInfoMap[propertyInfo.Name] = propertyInfo;
				optionNames = ListExtensions.AsReadOnly(optionPropertyInfoMap.Keys.ToArray());
				isOptionPropertyInfoMapReady = true;
			}
		}


		/// <summary>
		/// Get or set commands after executing <see cref="Command"/>.
		/// </summary>
		public IList<string> TeardownCommands
		{
			get => this.teardownCommands ?? emptyCommands;
			set => this.teardownCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		/// <summary>
		/// Get or set user name.
		/// </summary>
		public string? UserName { get; set; }


		/// <summary>
		/// Get or set whether using text shell to run command or not.
		/// </summary>
		public bool UseTextShell { get; set; }


		/// <summary>
		/// Get or set URI to connect.
		/// </summary>
		public Uri? Uri { get; set; }


		/// <summary>
		/// Path of working directory.
		/// </summary>
		public string? WorkingDirectory { get; set; }
	}


	/// <summary>
	/// Extensions for <see cref="ILogDataSourceProvider"/>.
	/// </summary>
	static class LogDataSourceProviderExtensions
	{
		/// <summary>
		/// Check whether given option is reqired for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/>.</param>
		/// <param name="optionName">Name of option to check.</param>
		/// <returns>True if given option is reqired for creating <see cref="ILogDataSource"/>.</returns>
		public static bool IsSourceOptionRequired(this ILogDataSourceProvider provider, string optionName) => provider.RequiredSourceOptions.Contains(optionName);


		/// <summary>
		/// Check whether given option is supported for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/>.</param>
		/// <param name="optionName">Name of option to check.</param>
		/// <returns>True if given option is supported for creating <see cref="ILogDataSource"/>.</returns>
		public static bool IsSourceOptionSupported(this ILogDataSourceProvider provider, string optionName) => provider.SupportedSourceOptions.Contains(optionName);
	}
}
