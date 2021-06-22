using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Implementation of <see cref="StandardOutputLogDataSource"/>.
	/// </summary>
	class StandardOutputLogDataSource : BaseLogDataSource
	{
		// Fields.
		string? commandFileOnReady;
		readonly LogDataSourceOptions options;


		/// <summary>
		/// Initialize new <see cref="StandardOutputLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="StandardOutputLogDataSourceProvider"/> which creates this instane.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create source.</param>
		internal StandardOutputLogDataSource(StandardOutputLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
			this.options = options;
		}


		// Open reader core.
		protected override LogDataSourceState OpenReaderCore(out TextReader? reader)
		{
			if (commandFileOnReady == null)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			using var process = new Process();
			process.StartInfo.FileName = commandFileOnReady;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.Start();
			reader = process.StandardOutput;
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare core.
		protected override LogDataSourceState PrepareCore()
		{
			if (string.IsNullOrEmpty(options.Command))
				return LogDataSourceState.SourceNotFound;
			var regex = new Regex("^(?<ExecutableCommand>([^\\s\"]*)|\"([^\\s])*\")[\\s$]");
			var match = regex.Match(options.Command);
			if (!match.Success)
				return LogDataSourceState.SourceNotFound;
			var commandGroup = match.Groups["ExecutableCommand"];
			var command = options.Command.Substring(0, commandGroup.Length - 1);
			var args = options.Command.Substring(commandGroup.Length);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !command.EndsWith(".exe"))
				command += ".exe";
			if (Path.IsPathRooted(command))
			{
				if (File.Exists(command))
				{
					this.Logger.LogDebug($"Command file found: {command}");
					this.commandFileOnReady = command;
					return LogDataSourceState.ReadyToOpenReader;
				}
			}
			else
			{
				if (options.WorkingDirectory != null)
				{
					string commandFile = Path.Combine(options.WorkingDirectory, command);
					if (File.Exists(commandFile))
					{
						this.Logger.LogDebug($"Command file from working directory: {commandFile}");
						this.commandFileOnReady = commandFile;
						return LogDataSourceState.ReadyToOpenReader;
					}
				}
				var environmentPaths = Environment.GetEnvironmentVariable("PATH");
				if (environmentPaths != null)
				{
					foreach (var environmentPath in environmentPaths.Split(Path.PathSeparator))
					{
						string commandFile = Path.Combine(environmentPath, command);
						if (File.Exists(commandFile))
						{
							this.Logger.LogDebug($"Command file from environment path: {commandFile}");
							this.commandFileOnReady = commandFile;
							return LogDataSourceState.ReadyToOpenReader;
						}
					}
				}
			}
			return LogDataSourceState.SourceNotFound;
		}
	}
}
