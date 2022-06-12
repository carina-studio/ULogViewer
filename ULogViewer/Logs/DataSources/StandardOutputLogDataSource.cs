using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
		// Constants.
		const uint SCS_32BIT_BINARY = 0; // 32-bit Windows application
		const uint SCS_64BIT_BINARY = 6; // 64-bit Windows application


		// Fields.
		volatile string? arguments;
		volatile string? commandFileOnReady;
		static readonly Regex regex = new Regex("^(?<ExecutableCommand>([^\\s\"]*)|\"([^\\s])*\")[\\s$]");
		Task? teardownCommandsTask;


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
					var isSameArchExecutable = Global.Run(() =>
					{
						if (Platform.IsWindows)
						{
							try
							{
								if (GetBinaryType(executablePath, out var binaryType))
								{
									var is64BitExe = binaryType == SCS_64BIT_BINARY;
									return Environment.Is64BitProcess == is64BitExe;
								}
							}
							catch
							{ }
						}
						return (bool?)null;
					});
					process.StartInfo.Let(it =>
					{
						if (isSameArchExecutable == false && Platform.IsWindows)
						{
							it.Arguments = $"/c \"{executablePath}\" {args}";
							it.FileName = "cmd";
						}
						else
						{
							it.Arguments = args ?? "";
							it.FileName = executablePath.AsNonNull();
						}
						it.CreateNoWindow = true;
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


		// Get type of executable (Windows).
		[DllImport("Kernel32")]
		static extern bool GetBinaryType(string? applicationName, out uint binaryType);


		// Reader closed.
		protected override void OnReaderClosed()
		{
			base.OnReaderClosed();
			var options = this.CreationOptions;
			if (options.TeardownCommands.IsNotEmpty())
			{
				this.SynchronizationContext.Post(async () =>
				{
					if (this.teardownCommandsTask != null)
						await this.teardownCommandsTask;
					this.Logger.LogWarning("Start executing teardown commands");
					var taskCompletionSource = new TaskCompletionSource();
					this.teardownCommandsTask = taskCompletionSource.Task;
					foreach (var command in options.TeardownCommands)
					{
						await this.TaskFactory.StartNew(() =>
							this.ExecuteCommandAndWaitForExit(command, options.WorkingDirectory));
					}
					this.Logger.LogWarning("Complete executing teardown commands");
					this.teardownCommandsTask = null;
					taskCompletionSource.SetResult();
				});
			}
		}


		// Open reader core.
		protected override async Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
		{
			// check state
			if (commandFileOnReady == null)
				return (LogDataSourceState.SourceNotFound, null);

			// wait for teardown commands
			if (this.teardownCommandsTask != null)
			{
				this.Logger.LogWarning("Wait for teardown commands");
				await this.teardownCommandsTask;
			}

			// execute setup commands
			var options = this.CreationOptions;
			if (options.SetupCommands.IsNotEmpty())
			{
				this.Logger.LogWarning("Start executing setup commands");
				var success = await this.TaskFactory.StartNew(() =>
				{
					foreach (var command in options.SetupCommands)
					{
						if (!this.ExecuteCommandAndWaitForExit(command, options.WorkingDirectory))
						{
							this.Logger.LogError($"Unable to execute setup command: {command}");
							return false;
						}
						if (cancellationToken.IsCancellationRequested)
							throw new TaskCanceledException();
					}
					return true;
				});
				if (!success)
					return (LogDataSourceState.UnclassifiedError, null);
				this.Logger.LogWarning("Complete executing setup commands");
			}

			// check executable type
			var isSameArchExecutable = await this.TaskFactory.StartNew(() =>
			{
				if (Platform.IsWindows)
				{
					try
					{
						if (GetBinaryType(this.commandFileOnReady, out var binaryType))
						{
							var is64BitExe = binaryType == SCS_64BIT_BINARY;
							return Environment.Is64BitProcess == is64BitExe;
						}
					}
					catch
					{ }
				}
				return (bool?)null;
			});

			// start process
			var process = new Process();
			if (isSameArchExecutable == false && Platform.IsWindows)
			{
				process.StartInfo.FileName = "cmd";
				if (this.CreationOptions.WorkingDirectory != null)
					process.StartInfo.WorkingDirectory = this.CreationOptions.WorkingDirectory;
				process.StartInfo.Arguments = $"/c \"{this.commandFileOnReady}\" {this.arguments}";
			}
			else
			{
				process.StartInfo.FileName = commandFileOnReady;
				if (this.CreationOptions.WorkingDirectory != null)
					process.StartInfo.WorkingDirectory = this.CreationOptions.WorkingDirectory;
				if (this.arguments != null)
					process.StartInfo.Arguments = this.arguments;
			}
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
			process.StartInfo.CreateNoWindow = true;
			try
			{
				if (!process.Start())
					return (LogDataSourceState.SourceNotFound, null);
			}
			catch (Win32Exception)
			{
				return (LogDataSourceState.SourceNotFound, null);
			}
			return (LogDataSourceState.ReaderOpened, new ProcessTextReader(this, process, options.IncludeStandardError));
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
			if (Platform.IsWindows && !executablePath.ToLower().EndsWith(".exe"))
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
				var environmentPaths = Global.Run(() =>
				{
					if (Platform.IsMacOS)
					{
						try
						{
							using var reader = new StreamReader("/etc/paths");
							var paths = new List<string>();
							var path = reader.ReadLine();
							while (path != null)
							{
								if (!string.IsNullOrWhiteSpace(path))
									paths.Add(path);
								path = reader.ReadLine();
							}
							return paths.IsNotEmpty() ? paths.ToArray() : null;
						}
						catch
						{
							return null;
						}
					}
					else
						return Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
				});
				if (environmentPaths != null)
				{
					foreach (var environmentPath in environmentPaths)
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
		protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken) => this.TaskFactory.StartNew(() =>
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
		});
	}
}
