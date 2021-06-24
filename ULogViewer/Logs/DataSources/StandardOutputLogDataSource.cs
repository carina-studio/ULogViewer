using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSource"/> for standard output.
	/// </summary>
	class StandardOutputLogDataSource : BaseLogDataSource
	{
		// Fields.
		volatile string? arguments;
		volatile string? commandFileOnReady;
		volatile Process? process;
		static readonly Regex regex = new Regex("^(?<ExecutableCommand>([^\\s\"]*)|\"([^\\s])*\")[\\s$]");


		/// <summary>
		/// Initialize new <see cref="StandardOutputLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="StandardOutputLogDataSourceProvider"/> which creates this instane.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create source.</param>
		public StandardOutputLogDataSource(StandardOutputLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
		}


		// Reader closed.
		protected override void OnReaderClosed()
		{
			base.OnReaderClosed();
			if (this.process != null)
			{
				this.process.Kill();
				this.process.WaitForExit();
				this.process = null;
			}
		}


		// Open reader core.
		protected override LogDataSourceState OpenReaderCore(out TextReader? reader)
		{
			if (commandFileOnReady == null)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			this.process = new Process();
			this.process.StartInfo.FileName = commandFileOnReady;
			if (this.CreationOptions.WorkingDirectory != null)
				this.process.StartInfo.WorkingDirectory = this.CreationOptions.WorkingDirectory;
			if (this.arguments != null)
				this.process.StartInfo.Arguments = this.arguments;
			this.process.StartInfo.UseShellExecute = false;
			this.process.StartInfo.RedirectStandardOutput = true;
			this.process.Start();
			reader = this.process.StandardOutput;
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare core.
		protected override LogDataSourceState PrepareCore()
		{
			if (string.IsNullOrEmpty(this.CreationOptions.Command))
				return LogDataSourceState.SourceNotFound;
			var match = StandardOutputLogDataSource.regex.Match(this.CreationOptions.Command);
			if (!match.Success)
				return LogDataSourceState.SourceNotFound;
			var commandGroup = match.Groups["ExecutableCommand"];
			var command = this.CreationOptions.Command.Substring(0, commandGroup.Length);
			this.arguments = this.CreationOptions.Command.Substring(commandGroup.Length);
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
				if (this.CreationOptions.WorkingDirectory != null)
				{
					string commandFile = Path.Combine(this.CreationOptions.WorkingDirectory, command);
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
