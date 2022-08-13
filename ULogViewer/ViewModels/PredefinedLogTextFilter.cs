using CarinaStudio.AppSuite.Data;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Predefined log text filter.
	/// </summary>
	class PredefinedLogTextFilter : BaseProfile<IULogViewerApplication>
	{
		// Fields.
		Regex? regex;


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
			this.Name = name;
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


		/// <inheritdoc/>
		public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
			profile is PredefinedLogTextFilter filter
			&& filter.Name == this.Name
			&& filter.Regex.ToString() == this.Regex.ToString()
			&& filter.Regex.Options == this.Regex.Options;


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
			var ignoreCase = false;
			if (element.TryGetProperty("IgnoreCase", out var jsonValue))
				ignoreCase = jsonValue.GetBoolean();
			var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
			if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseCompiledRegex))
				options |= RegexOptions.Compiled;
			this.Name = element.GetProperty(nameof(Name)).GetString();
			this.regex = new Regex(element.GetProperty(nameof(Regex)).GetString().AsNonNull(), options);
		}


		/// <inheritdoc/>
		protected override void OnSave(Utf8JsonWriter writer, bool includeId)
		{
			writer.WriteStartObject();
			this.Name?.Let(it =>
				writer.WriteString(nameof(Name), it));
			if (includeId)
				writer.WriteString(nameof(Id), this.Id);
			this.regex?.Let(it =>
			{
				if ((it.Options & RegexOptions.IgnoreCase) != 0)
					writer.WriteBoolean("IgnoreCase", true);
				writer.WriteString(nameof(Regex), it.ToString());
			});
			writer.WriteEndObject();
		}
	}
}
