using CarinaStudio.Collections;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSource"/> for standard output.
	/// </summary>
	class StandardOutputLogDataSource : BaseLogDataSource
	{
		// TextReader implementations.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly Process process;
			readonly TextReader stdoutReader;

			// Constructor.
			public ReaderImpl(Process process)
			{
				this.process = process;
				this.stdoutReader = process.StandardOutput;
			}

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				if (disposing)
					this.stdoutReader.Dispose();
				Global.RunWithoutError(() => this.process.Kill());
				Global.RunWithoutError(() => this.process.WaitForExit(1000));
				base.Dispose(disposing);
			}

			// Implementations.
			public override string? ReadLine() => this.stdoutReader.ReadLine();
		}


		// Fields.
		volatile string? arguments;
		volatile string? commandFileOnReady;
		volatile bool isExecutingTeardownCommands;
		static readonly Regex regex = new Regex("^(?<ExecutableCommand>([^\\s\"]*)|\"([^\\s])*\")[\\s$]");
		readonly object teardownCommandsLock = new object();


		/// <summary>
		/// Initialize new <see cref="StandardOutputLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="StandardOutputLogDataSourceProvider"/> which creates this instane.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create source.</param>
		public StandardOutputLogDataSource(StandardOutputLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{ }


		// Execute command and wait for exit.
		bool ExecuteCommandAndWaitForExit(string command, string? workingDirectory, int timeoutMillis = 10000)
		{
			if (!this.ParseCommand(command, workingDirectory, out var executablePath, out var args))
				return false;
			try
			{
				var process = new Process().Also(process =>
				{
					process.StartInfo.Let(it =>
					{
						it.Arguments = args ?? "";
						it.CreateNoWindow = true;
						it.FileName = executablePath.AsNonNull();
						it.UseShellExecute = false;
						it.WorkingDirectory = workingDirectory ?? "";
					});
				});
				if (!process.Start())
				{
					this.Logger.LogWarning($"Unable to execute command: {command}");
					return false;
				}
				process.WaitForExit(timeoutMillis);
				Global.RunWithoutError(process.Kill);
				return true;
			}
			catch(Exception ex)
			{
				this.Logger.LogWarning(ex, $"Unable to execute command: {command}");
				return false;
			}
		}


		// Reader closed.
		protected override void OnReaderClosed()
		{
			base.OnReaderClosed();
			var options = this.CreationOptions;
			if (options.TeardownCommands.IsNotEmpty())
			{
				Task.Run(() =>
				{
					lock (this.teardownCommandsLock)
					{
						this.Logger.LogWarning("Start executing teardown commands");
						this.isExecutingTeardownCommands = true;
					}
					foreach (var command in options.TeardownCommands)
						this.ExecuteCommandAndWaitForExit(command, options.WorkingDirectory);
					lock (this.teardownCommandsLock)
					{
						this.Logger.LogWarning("Complete executing teardown commands");
						this.isExecutingTeardownCommands = false;
						Monitor.PulseAll(this.teardownCommandsLock);
					}
				});
			}
		}


		// Open reader core.
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			// check state
			if (commandFileOnReady == null)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}

			// wait for teardown commands
			lock (this.teardownCommandsLock)
			{
				if (this.isExecutingTeardownCommands)
				{
					this.Logger.LogWarning("Wait for teardown commands");
					Monitor.Wait(this.teardownCommandsLock);
				}
			}

			// execute setup commands
			var options = this.CreationOptions;
			if (options.SetupCommands.IsNotEmpty())
			{
				this.Logger.LogWarning("Start executing setup commands");
				foreach (var command in options.SetupCommands)
				{
					if (!this.ExecuteCommandAndWaitForExit(command, options.WorkingDirectory))
					{
						this.Logger.LogError($"Unable to execute setup command: {command}");
						reader = null;
						return LogDataSourceState.UnclassifiedError;
					}
				}
				this.Logger.LogWarning("Complete executing setup commands");
			}

			// start process
			var process = new Process();
			process.StartInfo.FileName = commandFileOnReady;
			if (this.CreationOptions.WorkingDirectory != null)
				process.StartInfo.WorkingDirectory = this.CreationOptions.WorkingDirectory;
			if (this.arguments != null)
				process.StartInfo.Arguments = this.arguments;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			reader = new ReaderImpl(process);
			return LogDataSourceState.ReaderOpened;
		}


		// Parse command.
		bool ParseCommand(string? command, string? workingDirectory, out string? executablePath, out string? arguments)
		{
			// check command
			executablePath = null;
			arguments = null;
			if (string.IsNullOrWhiteSpace(command))
				return false;

			// find executable path
			var match = regex.Match(command);
			if (!match.Success)
				return false;
			var commandGroup = match.Groups["ExecutableCommand"];
			executablePath = command.Substring(0, commandGroup.Length);
			arguments = command.Substring(commandGroup.Length);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !executablePath.EndsWith(".exe"))
				executablePath += ".exe";
			if (Path.IsPathRooted(executablePath))
				return File.Exists(executablePath);
			else
			{
				if (workingDirectory != null)
				{
					string commandFile = Path.Combine(workingDirectory, executablePath);
					if (File.Exists(commandFile))
					{
						executablePath = commandFile;
						return true;
					}
				}
				var environmentPaths = Environment.GetEnvironmentVariable("PATH");
				if (environmentPaths != null)
				{
					foreach (var environmentPath in environmentPaths.Split(Path.PathSeparator))
					{
						string commandFile = Path.Combine(environmentPath, executablePath);
						if (File.Exists(commandFile))
						{
							executablePath = commandFile;
							return true;
						}
					}
				}
			}

			// cannot find executable
			executablePath = null;
			arguments = null;
			return false;
		}


		// Prepare core.
		protected override LogDataSourceState PrepareCore()
		{
			if (!this.ParseCommand(this.CreationOptions.Command, this.CreationOptions.WorkingDirectory, out var executablePath, out var arguments))
			{
				this.Logger.LogError($"Unable to locate executable for command: {this.CreationOptions.Command}");
				return LogDataSourceState.SourceNotFound;
			}
			this.Logger.LogDebug($"Executable found: {executablePath}");
			this.commandFileOnReady = executablePath;
			this.arguments = arguments;
			return LogDataSourceState.ReadyToOpenReader;
		}
	}
}
