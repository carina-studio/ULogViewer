using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Predefined log text filter.
	/// </summary>
	class PredefinedLogTextFilter : IApplicationObject, INotifyPropertyChanged
	{
		// Static fields.
		static readonly TaskFactory savingTaskFactory = new TaskFactory(new FixedThreadsTaskScheduler(1));


		// Fields.
		string? fileName;
		string id = "";
		string name;
		Regex regex;
		int savedVersion;
		int version;


		/// <summary>
		/// Initialize new <see cref="PredefinedLogTextFilter"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="name">Name.</param>
		/// <param name="regex"><see cref="Regex"/> to filter log text.</param>
		public PredefinedLogTextFilter(IULogViewerApplication app, string name, Regex regex)
		{
			this.Application = app;
			this.name = name;
			this.regex = regex;
			++this.version;
		}


		/// <summary>
		/// Get application.
		/// </summary>
		public IULogViewerApplication Application { get; }


		/// <summary>
		/// Change ID of filter.
		/// </summary>
		internal void ChangeId()
		{
			this.VerifyAccess();
			this.id = PredefinedLogTextFilters.GenerateId();
			++this.version;
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
		}


		/// <summary>
		/// Delete the file which the filter be saved to.
		/// </summary>
		/// <returns>Task of deleting operation.</returns>
		public async Task DeleteFileAsync()
		{
			var fileName = this.fileName;
			if (fileName == null)
				return;
			await savingTaskFactory.StartNew(() => Global.RunWithoutError(() => File.Delete(fileName)));
			this.fileName = null;
			this.savedVersion = 0;
		}


		/// <summary>
		/// Get name of file which instance has been saved to or loaded from.
		/// </summary>
		public string? FileName { get => this.fileName; }


		/// <summary>
		/// Find valid file name for saving <see cref="PredefinedLogTextFilter"/> to file in given directory asynchronously.
		/// </summary>
		/// <param name="directoryName">Name of directory contains file.</param>
		/// <returns>Found file name.</returns>
		public async Task<string> FindValidFileNameAsync(string directoryName)
		{
			// generate base name for file
			var baseNameBuilder = new StringBuilder(this.name);
			if (baseNameBuilder.Length > 0)
			{
				for (var i = baseNameBuilder.Length - 1; i >= 0; --i)
				{
					var c = baseNameBuilder[i];
					if (char.IsWhiteSpace(c))
						baseNameBuilder[i] = '_';
					if (!char.IsDigit(c) && !char.IsLetter(c) && c != '-')
						baseNameBuilder[i] = '_';
				}
			}
			else
				baseNameBuilder.Append("Empty");
			var baseName = baseNameBuilder.ToString();

			// find valid file name
			return await Task.Run(() =>
			{
				var fileName = Path.Combine(directoryName, $"{baseName}.json");
				if (!File.Exists(fileName) && !Directory.Exists(fileName))
					return fileName;
				for (var i = 1; i <= 1000; ++i)
				{
					fileName = Path.Combine(directoryName, $"{baseName}_{i}.json");
					if (!File.Exists(fileName) && !Directory.Exists(fileName))
						return fileName;
				}
				throw new ArgumentException($"Unable to find proper file name for '{this.name}' in directory '{directoryName}'.");
			});
		}


		/// <summary>
		/// Get unique ID of filter.
		/// </summary>
		public string Id { get => this.id; }


		/// <summary>
		/// Load <see cref="PredefinedLogTextFilter"/> from file asynchronously.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="fileName">File name.</param>
		/// <returns><see cref="PredefinedLogTextFilter"/>.</returns>
		public static async Task<PredefinedLogTextFilter> LoadAsync(IULogViewerApplication app, string fileName)
		{
			var name = "";
			var id = "";
			var regex = (Regex?)null;
			await Task.Run(() =>
			{
				using var reader = new StreamReader(fileName, Encoding.UTF8);
				using var jsonDocument = JsonDocument.Parse(reader.ReadToEnd());
				var jsonObject = jsonDocument.RootElement;
				var ignoreCase = false;
				if (jsonObject.TryGetProperty("IgnoreCase", out var jsonValue))
					ignoreCase = jsonValue.GetBoolean();
				name = jsonObject.GetProperty(nameof(Name)).GetString().AsNonNull();
				regex = new Regex(jsonObject.GetProperty(nameof(Regex)).GetString().AsNonNull(), ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
				if (jsonObject.TryGetProperty("Id", out jsonValue))
					id = jsonValue.GetString().AsNonNull();
			});
			return new PredefinedLogTextFilter(app, name, regex.AsNonNull()).Also(it =>
			{
				it.fileName = fileName;
				it.id = id;
				it.savedVersion = it.version;
			});
		}


		/// <summary>
		/// Get or set name.
		/// </summary>
		public string Name
		{
			get => this.name;
			set
			{
				this.VerifyAccess();
				if (this.name == value)
					return;
				this.name = value;
				++this.version;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
			}
		}


		/// <summary>
		/// Get or set <see cref="Regex"/> to filter log text.
		/// </summary>
		public Regex Regex
		{
			get => this.regex;
			set
			{
				this.VerifyAccess();
				if (this.regex.ToString() == value.ToString() && this.regex.Options == value.Options)
					return;
				this.regex = value;
				++this.version;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Regex)));
			}
		}


		/// <summary>
		/// Save <see cref="PredefinedLogTextFilter"/> to file asynchronously.
		/// </summary>
		/// <param name="fileName">File name.</param>
		/// <returns>Task of saving operation.</returns>
		public async Task SaveAsync(string fileName)
		{
			// check state
			this.VerifyAccess();
			if (this.fileName == fileName && this.savedVersion == this.version)
				return;

			// save to file
			var prevFileName = this.fileName;
			var name = this.name;
			var id = this.id;
			var regex = this.regex;
			var version = this.version;
			await savingTaskFactory.StartNew(() =>
			{
				// delete previous file
				if (prevFileName != null && prevFileName != fileName)
					Global.RunWithoutError(() => File.Delete(prevFileName));

				// create directory
				Path.GetDirectoryName(fileName)?.Let(dirName => Directory.CreateDirectory(dirName));

				// write to file
				using var stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
				using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });
				writer.WriteStartObject();
				writer.WriteString(nameof(Name), name);
				writer.WriteString(nameof(Id), id);
				if ((regex.Options & RegexOptions.IgnoreCase) != 0)
					writer.WriteBoolean("IgnoreCase", true);
				writer.WriteString(nameof(Regex), regex.ToString());
				writer.WriteEndObject();
			});

			// update state
			this.fileName = fileName;
			this.savedVersion = version;
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
