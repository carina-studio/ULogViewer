using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			this.Logs = new List<DisplayableLog>(logs).AsReadOnly();
		}


		/// <summary>
		/// Get or set name of file to save logs to.
		/// </summary>
		public string? FileName { get; set; }


		/// <summary>
		/// Check whether <see cref="FileName"/> has been set or not.
		/// </summary>
		public bool HasFileName { get => !string.IsNullOrWhiteSpace(this.FileName); }


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
		// Static fields.
		static readonly IDictionary<string, string> EmptyLogPropertyMap = DictionaryExtensions.AsReadOnly(new Dictionary<string, string>());


		// Fields.
		IDictionary<string, string> logPropertyMap = EmptyLogPropertyMap;


		/// <summary>
		/// Initialize new <see cref="JsonLogsSavingOptions"/> instance.
		/// </summary>
		/// <param name="logs">Logs to save.</param>
		public JsonLogsSavingOptions(IEnumerable<DisplayableLog> logs) : base(logs)
		{ }


		/// <summary>
		/// Get or set map to convert name of property of <see cref="Log"/> to name of JSON property.
		/// </summary>
		public IDictionary<string, string> LogPropertyMap
		{
			get => this.logPropertyMap;
			set => this.logPropertyMap = DictionaryExtensions.AsReadOnly(new Dictionary<string, string>(value));
		}
	}
}
