using CarinaStudio.AppSuite.Data;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Predefined log text filter.
/// </summary>
class PredefinedLogTextFilter : BaseProfile<IULogViewerApplication>
{
	// Fields.
	string? groupName;
	Regex? regex;
	bool? saveWithGroupName;


	// Constructor.
	PredefinedLogTextFilter(IULogViewerApplication app, string id) : base(app, id, false)
	{ }


	/// <summary>
	/// Initialize new <see cref="PredefinedLogTextFilter"/> instance.
	/// </summary>
	/// <param name="app">Application.</param>
	/// <param name="name">Name.</param>
	/// <param name="regex"><see cref="Regex"/> to filter log text.</param>
	public PredefinedLogTextFilter(IULogViewerApplication app, string name, Regex regex) : base(app, PredefinedLogTextFilterManager.Default.GenerateProfileId(), false)
	{
		// ReSharper disable VirtualMemberCallInConstructor
		this.Name = name;
		// ReSharper restore VirtualMemberCallInConstructor
		this.regex = regex;
	}


	/// <summary>
	/// Change ID of filter.
	/// </summary>
	internal void ChangeId()
	{
		this.VerifyAccess();
		this.Id = PredefinedLogTextFilterManager.Default.GenerateProfileId();
	}


	/// <summary>
	/// Make sure that given name is correct group name.
	/// </summary>
	/// <param name="name">Name of group.</param>
	/// <returns>Corrected group name.</returns>
	public static string? CorrectGroupName(string? name) =>
		CorrectGroupName(name, out _);
	
	
	/// <summary>
	/// Make sure that given name is correct group name.
	/// </summary>
	/// <param name="name">Name of group.</param>
	/// <param name="corrected">True if name has been corrected.</param>
	/// <returns>Corrected group name.</returns>
	public static string? CorrectGroupName(string? name, out bool corrected)
	{
		corrected = false;
		if (string.IsNullOrWhiteSpace(name))
			return null;
		var correctedName = name;
		if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]))
			correctedName = correctedName.Trim();
		correctedName = correctedName.Replace('/', '-').Replace('\\', '-');
		corrected = correctedName != name;
		return correctedName;
	}


	/// <inheritdoc/>
	public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
		profile is PredefinedLogTextFilter filter
		&& filter.Name == this.Name
		&& filter.groupName == this.groupName
		&& filter.Regex.ToString() == this.Regex.ToString()
		&& filter.Regex.Options == this.Regex.Options;

	
	/// <summary>
	/// Get or set name of group which contains the filter.
	/// </summary>
	public string? GroupName
	{
		get => this.groupName;
		set
		{
			this.VerifyAccess();
			this.VerifyBuiltIn();
			if (string.IsNullOrWhiteSpace(value))
				value = null;
			if (this.groupName == value)
				return;
			this.groupName = value;
			this.OnPropertyChanged(nameof(GroupName));
		}
	}
	
	
	/// <summary>
	/// Check whether internal data has been just upgraded or not.
	/// </summary>
	public bool IsDataUpgraded { get; private set; }


	/// <summary>
	/// Load <see cref="PredefinedLogTextFilter"/> from file asynchronously.
	/// </summary>
	/// <param name="app">Application.</param>
	/// <param name="fileName">File name.</param>
	/// <returns><see cref="PredefinedLogTextFilter"/>.</returns>
	public static async Task<PredefinedLogTextFilter> LoadAsync(IULogViewerApplication app, string fileName)
	{
		// load JSON data
		using var jsonDocument = await ProfileExtensions.IOTaskFactory.StartNew(() =>
		{
			using var reader = new StreamReader(fileName, Encoding.UTF8);
			return JsonDocument.Parse(reader.ReadToEnd());
		});
		var element = jsonDocument.RootElement;
		if (element.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Root element must be an object.");
		
		// get ID
		var id = element.TryGetProperty(nameof(Id), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
			? jsonProperty.GetString().AsNonNull()
			: PredefinedLogTextFilterManager.Default.GenerateProfileId();

		// load
		var filter = new PredefinedLogTextFilter(app, id);
		filter.Load(element);
		return filter;
	}


	/// <summary>
	/// Get or set <see cref="Regex"/> to filter log text.
	/// </summary>
	public Regex Regex
	{
		get => this.regex.AsNonNull();
		set
		{
			this.VerifyAccess();
			if (this.regex?.ToString() == value.ToString() && this.regex.Options == value.Options)
				return;
			this.regex = value;
			this.OnPropertyChanged(nameof(Regex));
		}
	}


	/// <inheritdoc/>
	protected override void OnLoad(JsonElement element)
	{
		// name
		this.Name = element.GetProperty(nameof(Name)).GetString();
		
		// regex
		var ignoreCase = false;
		if (element.TryGetProperty("IgnoreCase", out var jsonValue))
			ignoreCase = jsonValue.GetBoolean();
		var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
		if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseCompiledRegex))
			options |= RegexOptions.Compiled;
		this.regex = new Regex(element.GetProperty(nameof(Regex)).GetString().AsNonNull(), options);
		
		// group name
		var groupName = element.TryGetProperty(nameof(GroupName), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
			? jsonProperty.GetString()?.Let(it => string.IsNullOrWhiteSpace(it) ? null : it)
			: null;
		this.groupName = CorrectGroupName(groupName, out var isGroupNameCorrected);
		if (isGroupNameCorrected)
			this.IsDataUpgraded = true;
	}


	/// <inheritdoc/>
	protected override void OnSave(Utf8JsonWriter writer, bool includeId)
	{
		writer.WriteStartObject();
		this.Name?.Let(it =>
			writer.WriteString(nameof(Name), it));
		if (includeId)
			writer.WriteString(nameof(Id), this.Id);
		if (this.saveWithGroupName != false && this.groupName is not null)
			writer.WriteString(nameof(GroupName), this.groupName);
		this.regex?.Let(it =>
		{
			if ((it.Options & RegexOptions.IgnoreCase) != 0)
				writer.WriteBoolean("IgnoreCase", true);
			writer.WriteString(nameof(Regex), it.ToString());
		});
		writer.WriteEndObject();
	}


	/// <summary>
	/// Save filter to file asynchronously.
	/// </summary>
	/// <param name="fileName">File name.</param>
	/// <param name="includeId">True to save with ID.</param>
	/// <param name="includeGroupName">True to save with group name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Task of saving filter.</returns>
	public async Task SaveAsync(string fileName, bool includeId, bool includeGroupName, CancellationToken cancellationToken)
	{
		this.VerifyAccess();
		this.saveWithGroupName = includeGroupName;
		try
		{
			await this.SaveAsync(fileName, includeId, cancellationToken);
		}
		finally
		{
			this.saveWithGroupName = null;
		}
	}
}
