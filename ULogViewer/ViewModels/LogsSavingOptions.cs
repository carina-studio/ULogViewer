using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Options of saving logs.
	/// </summary>
	class LogsSavingOptions
	{
		/// <summary>
		/// Initialize new <see cref="LogsSavingOptions"/> instance
		/// </summary>
		/// <param name="logs">Logs to save.</param>
		public LogsSavingOptions(IEnumerable<DisplayableLog> logs)
		{
			this.Logs = ImmutableList.CreateRange(logs);
		}


		/// <summary>
		/// Get or set name of file to save logs to.
		/// </summary>
		public string? FileName { get; set; }


		/// <summary>
		/// Check whether <see cref="FileName"/> has been set or not.
		/// </summary>
		public bool HasFileName => !string.IsNullOrWhiteSpace(this.FileName);


		/// <summary>
		/// Get logs to save.
		/// </summary>
		public IList<DisplayableLog> Logs { get; }
	}


	/// <summary>
	/// <see cref="LogsSavingOptions"/> for saving in JSON format.
	/// </summary>
	class JsonLogsSavingOptions : LogsSavingOptions
	{
		/// <summary>
		/// Initialize new <see cref="JsonLogsSavingOptions"/> instance.
		/// </summary>
		/// <param name="logs">Logs to save.</param>
		public JsonLogsSavingOptions(IEnumerable<DisplayableLog> logs) : base(logs)
		{ }


		/// <summary>
		/// Get or set map to convert name of property of <see cref="Logs.Log"/> to name of JSON property.
		/// </summary>
		public IDictionary<string, string> LogPropertyMap
		{
			get;
			set => field = ImmutableDictionary.CreateRange(value);
		} = ImmutableDictionary<string, string>.Empty;
	}
}
