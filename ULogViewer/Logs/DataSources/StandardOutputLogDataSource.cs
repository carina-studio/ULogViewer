using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
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
		const int DeleteTempWorkingDirRetryingCount = 10;


		// Fields.
		volatile string? arguments;
		volatile string? commandFileOnReady;
		static readonly Regex regex = new("^(?<ExecutableCommand>([^\\s\"]*)|\"([^\\s])*\")(\\s|$)");
		Task? teardownCommandsTask;
		string? tempWorkingDirectory;


		/// <summary>
		/// Initialize new <see cref="StandardOutputLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="StandardOutputLogDataSourceProvider"/> which creates this instane.</param>
		/// <param name="options"><see cref="LogDataSourceOptions"/> to create source.</param>
		public StandardOutputLogDataSource(StandardOutputLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{ }


		// Delete temporary working directory.
		void DeleteTempWorkingDirectory(string path) => Task.Run(() =>
		{
			for (var i = DeleteTempWorkingDirRetryingCount; i > 0; --i)
			{
				try
				{
					Directory.Delete(path, true);
					this.Logger.LogTrace("Temp working directory '{path}' deleted", path);
					break;
				}
				catch (Exception ex)
				{
					if (i > 1)
					{
						this.Logger.LogWarning(ex, "Failed to delete temp working directory '{path}', try again later", path);
						Thread.Sleep(1000);
					}
					else
						this.Logger.LogError(ex, "Failed to delete temp working directory '{path}'", path);
				}
			}
		});


		// Execute command and wait for exit.
		bool ExecuteCommandAndWaitForExit(string command, string? workingDirectory, int timeoutMillis = 10000)
		{
			var useTextShell = this.CreationOptions.UseTextShell;
			var executablePath = (string?)null;
			var args = (string?)null;
			if (!useTextShell && !ParseCommand(command, workingDirectory, out executablePath, out args))
			{
				if (TryGettingExecutableCommand(command, out var exe))
					this.GenerateMessage(LogDataSourceMessageType.Error, this.Application.GetFormattedString("StandardOutputLogDataSource.CommandNotFound", exe));
				return false;
			}
			try
			{
				var process = new Process().Also(process =>
				{
					process.StartInfo.Let(it =>
					{
						if (useTextShell && TextShellManager.Default.TryGetDefaultTextShellPath(out var shell, out var shellPath))
						{
							it.Arguments = PrepareTextShellArguments(shell, command);
							it.FileName = shellPath;
							it.RedirectStandardInput = true;
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
					this.Logger.LogWarning("Unable to execute command: {command}", command);
					if (TryGettingExecutableCommand(command, out var exe))
						this.GenerateMessage(LogDataSourceMessageType.Error, this.Application.GetFormattedString("StandardOutputLogDataSource.FailedToExecuteCommand", exe));
					return false;
				}
				process.WaitForExit(timeoutMillis);
				Global.RunWithoutError(process.Kill);
				return true;
			}
			catch(Exception ex)
			{
				this.Logger.LogWarning(ex, "Unable to execute command: {command}", command);
				if (TryGettingExecutableCommand(command, out var exe))
					this.GenerateMessage(LogDataSourceMessageType.Error, this.Application.GetFormattedString("StandardOutputLogDataSource.FailedToExecuteCommand", exe));
				return false;
			}
		}


		// Reader closed.
		protected override void OnReaderClosed()
		{
			base.OnReaderClosed();
			var options = this.CreationOptions;
			var tempWorkingDirectory = this.tempWorkingDirectory;
			if (options.TeardownCommands.IsNotEmpty())
			{
				var workingDirectory = tempWorkingDirectory ?? options.WorkingDirectory;
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
							this.ExecuteCommandAndWaitForExit(command, workingDirectory));
					}
					this.Logger.LogWarning("Complete executing teardown commands");
					if (tempWorkingDirectory != null)
					{
						this.Logger.LogTrace("Delete temp working directory '{tempWorkingDirectory}' after completing teardown commands", tempWorkingDirectory);
						this.DeleteTempWorkingDirectory(tempWorkingDirectory);
						this.tempWorkingDirectory = null;
					}
					this.teardownCommandsTask = null;
					taskCompletionSource.SetResult();
				});
			}
			else if (tempWorkingDirectory != null)
			{
				this.Logger.LogTrace("Delete temp working directory '{tempWorkingDirectory}' after closing reader", tempWorkingDirectory);
				this.DeleteTempWorkingDirectory(tempWorkingDirectory);
				this.tempWorkingDirectory = null;
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
			var workingDirectory = await options.WorkingDirectory.LetAsync(async it =>
			{
				if (string.IsNullOrWhiteSpace(it))
				{
					this.tempWorkingDirectory = await this.TaskFactory.StartNew(() =>
					{
						var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
						this.Logger.LogTrace("Create temp working directory '{path}'", path);
						Directory.CreateDirectory(path);
						return path;
					});
					if (cancellationToken.IsCancellationRequested)
					{
						var path = this.tempWorkingDirectory;
						this.tempWorkingDirectory = null;
						this.Logger.LogTrace("Delete temp working directory '{path}' because of cancellation has been requested", path);
						this.DeleteTempWorkingDirectory(path);
						throw new TaskCanceledException();
					}
					return this.tempWorkingDirectory;
				}
				return it;
			});
			if (options.SetupCommands.IsNotEmpty())
			{
				this.Logger.LogWarning("Start executing setup commands");
				var success = await this.TaskFactory.StartNew(() =>
				{
					foreach (var command in options.SetupCommands)
					{
						if (!this.ExecuteCommandAndWaitForExit(command, workingDirectory))
						{
							this.Logger.LogError("Unable to execute setup command: {command}", command);
							this.tempWorkingDirectory?.Let(it =>
							{
								this.Logger.LogTrace("Delete temp working directory '{directory}' because of failure of executing setup command", it);
								this.DeleteTempWorkingDirectory(it);
								this.tempWorkingDirectory = null;
							});
							return false;
						}
						if (cancellationToken.IsCancellationRequested)
						{
							this.tempWorkingDirectory?.Let(it =>
							{
								this.Logger.LogTrace("Delete temp working directory '{directory}' because of cancellation has been requested", it);
								this.DeleteTempWorkingDirectory(it);
								this.tempWorkingDirectory = null;
							});
							throw new TaskCanceledException();
						}
					}
					return true;
				});
				if (!success)
					return (LogDataSourceState.UnclassifiedError, null);
				this.Logger.LogWarning("Complete executing setup commands");
			}

			// start process
			var process = new Process();
			process.StartInfo.Let(startInfo =>
			{
				startInfo.FileName = commandFileOnReady;
				startInfo.WorkingDirectory = workingDirectory;
				if (this.arguments != null)
					startInfo.Arguments = this.arguments;
				startInfo.UseShellExecute = false;
				startInfo.RedirectStandardError = true;
				startInfo.RedirectStandardOutput = true;
				if (Platform.IsWindows)
				{
					var commandToCheck = Path.GetFileName(commandFileOnReady)?.ToLower();
					if (commandToCheck == "cmd"
						|| commandToCheck == "cmd.exe"
						|| commandToCheck == "powershell"
						|| commandToCheck == "powershell.exe")
					{
						startInfo.StandardErrorEncoding = CultureInfo.InstalledUICulture.ToString().Let(name =>
						{
							if (name.StartsWith("zh-"))
							{
								if (name.EndsWith("TW"))
									return System.Text.CodePagesEncodingProvider.Instance.GetEncoding(950); // Big5
								return System.Text.CodePagesEncodingProvider.Instance.GetEncoding(936); // GB2312
							}
							return System.Text.Encoding.UTF8;
						});
					}
					else
						startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
				}
				else
					startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
				startInfo.StandardOutputEncoding = startInfo.StandardErrorEncoding;
				startInfo.CreateNoWindow = true;
			});
			try
			{
				if (!process.Start())
				{
					this.tempWorkingDirectory?.Let(it =>
					{
						this.Logger.LogTrace("Delete temp working directory '{directory}' because of failure of starting process", it);
						this.DeleteTempWorkingDirectory(it);
						this.tempWorkingDirectory = null;
					});
					return (LogDataSourceState.SourceNotFound, null);
				}
			}
			catch (Win32Exception)
			{
				this.tempWorkingDirectory?.Let(it =>
				{
					this.Logger.LogTrace("Delete temp working directory '{directory}' because of failure of starting process", it);
					this.DeleteTempWorkingDirectory(it);
					this.tempWorkingDirectory = null;
				});
				return (LogDataSourceState.SourceNotFound, null);
			}
			var reader = new ProcessTextReader(this, process, options.IncludeStandardError).Let(it =>
			{
				if (options.FormatJsonData)
					return new FormattedJsonTextReader(it);
				return (TextReader)it;
			});
			return (LogDataSourceState.ReaderOpened, reader);
		}


		// Parse command.
		static bool ParseCommand(string? command, string? workingDirectory, [NotNullWhen(true)] out string? executablePath, [NotNullWhen(true)] out string? arguments)
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
			executablePath = command[0..commandGroup.Length];
			arguments = command[commandGroup.Length..^0];
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
					else if (Platform.IsWindows)
					{
						return new HashSet<string>(CarinaStudio.IO.PathEqualityComparer.Default).Also(pathSet =>
						{
							Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)?.Split(Path.PathSeparator)?.Let(it => pathSet.AddAll(it));
							Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)?.Split(Path.PathSeparator)?.Let(it => pathSet.AddAll(it));
						}).ToArray();
					}
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


		// Prepare arguments for running command by text shell.
		static string PrepareTextShellArguments(TextShell shell, string command) => shell switch
		{
			TextShell.CommandPrompt => $"/c {command}",
			TextShell.PowerShell => $"-NoLogo -Command {command}",
			_ => $"-c \"{command}\"",
		};


		// Prepare core.
		protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken) => this.TaskFactory.StartNew(() =>
		{
			if (this.CreationOptions.UseTextShell)
			{
				if (TextShellManager.Default.TryGetDefaultTextShellPath(out var shell, out var shellPath))
				{
					this.commandFileOnReady = shellPath;
					this.arguments = PrepareTextShellArguments(shell, this.CreationOptions.Command.AsNonNull());
					return LogDataSourceState.ReadyToOpenReader;
				}
				else
				{
					this.Logger.LogError("No text shell on system to run commands");
					return LogDataSourceState.SourceNotFound;
				}
			}
			if (!ParseCommand(this.CreationOptions.Command, this.CreationOptions.WorkingDirectory, out var executablePath, out var arguments))
			{
				this.Logger.LogError("Unable to locate executable for command: {command}", this.CreationOptions.Command);
				if (TryGettingExecutableCommand(this.CreationOptions.Command, out var exe))
					this.GenerateMessage(LogDataSourceMessageType.Error, this.Application.GetFormattedString("StandardOutputLogDataSource.CommandNotFound", exe));
				return LogDataSourceState.SourceNotFound;
			}
			this.Logger.LogDebug("Executable found: {executablePath}", executablePath);
			this.commandFileOnReady = executablePath;
			this.arguments = arguments;
			return LogDataSourceState.ReadyToOpenReader;
		});


		// Try getting executable name from command.
		static bool TryGettingExecutableCommand(string? command, out string exeCommand)
		{
			if (command == null)
			{
				exeCommand = "";
				return false;
			}
			var match = regex.Match(command);
			if (match.Success)
			{
				exeCommand = match.Groups["ExecutableCommand"].Value;
				return true;
			}
			exeCommand = "";
			return false;
		}
	}
}
