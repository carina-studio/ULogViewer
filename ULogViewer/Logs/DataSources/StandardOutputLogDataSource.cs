using CarinaStudio.Collections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
			string? bufferedLineFromStderr;
			string? bufferedLineFromStdout;
			volatile bool endOfStream;
			volatile bool isReadingLine;
			readonly Process process;
			readonly StandardOutputLogDataSource source;
			readonly object stderrReadingLock = new object();
			readonly TextReader? stdoutReader;
			readonly object stdoutReadingLock = new object();
			readonly object syncLock = new();

			// Constructor.
			public ReaderImpl(StandardOutputLogDataSource source, Process process, bool includeStderr)
			{
				this.process = process;
				this.source = source;
				if (includeStderr)
				{
					new Thread(() => this.ReadLinesFromProcess("stderr", process.BeginErrorReadLine, typeof(Process).GetEvent("ErrorDataReceived").AsNonNull(), ref this.bufferedLineFromStderr, this.stderrReadingLock))
					{
						IsBackground = true,
						Name = $"{nameof(StandardOutputLogDataSource)}-{source.Id}-stderr",
					}.Start();
					new Thread(() => this.ReadLinesFromProcess("stdout", process.BeginOutputReadLine, typeof(Process).GetEvent("OutputDataReceived").AsNonNull(), ref this.bufferedLineFromStdout, this.stdoutReadingLock))
					{
						IsBackground = true,
						Name = $"{nameof(StandardOutputLogDataSource)}-{source.Id}-stderr",
					}.Start();
				}
				else
					this.stdoutReader = process.StandardOutput;
			}

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				lock (this.syncLock)
				{
					this.endOfStream = true;
					this.isReadingLine = false;
					if (this.stdoutReader == null)
					{
						this.source.Logger.LogTrace("Notify to complete reading lines from stderr/stdout");
						Global.RunWithoutError(this.process.CancelErrorRead);
						Global.RunWithoutError(this.process.CancelOutputRead);
						lock (this.stderrReadingLock)
							Monitor.PulseAll(this.stderrReadingLock);
						lock (this.stdoutReadingLock)
							Monitor.PulseAll(this.stdoutReadingLock);
					}
				}
				if (disposing)
					this.stdoutReader?.Close();
				Global.RunWithoutError(() => this.process.Kill());
				Global.RunWithoutError(() => this.process.WaitForExit(1000));
				base.Dispose(disposing);
			}

			// Read line.
			public override string? ReadLine()
			{
				if (this.stdoutReader != null)
					return this.stdoutReader.ReadLine();
				if (this.endOfStream)
					return null;
				lock (this.syncLock)
				{
					// start reading line
					if (this.endOfStream)
						return null;
					if (this.isReadingLine)
						throw new InvalidOperationException();
					this.isReadingLine = true;
					Monitor.PulseAll(this.syncLock);

					// wait for reading line
					try
					{
						Monitor.Wait(this.syncLock);
					}
					finally
					{
						this.isReadingLine = false;
					}

					// get read line
					if (this.bufferedLineFromStderr != null)
					{
						var line = this.bufferedLineFromStderr;
						this.bufferedLineFromStderr = null;
						return line;
					}
					if (this.bufferedLineFromStdout != null)
					{
						var line = this.bufferedLineFromStdout;
						this.bufferedLineFromStdout = null;
						return line;
					}
					Monitor.PulseAll(this.syncLock);
					this.endOfStream = true;
					return null;
				}
			}

			// Read lines from given reader.
			void ReadLinesFromProcess(string name, Action beginReadLineAction, EventInfo lineReadEvent, ref string? bufferedLine, object readingLock)
			{
				// setup event handler to receive read line
				this.source.Logger.LogTrace($"Start reading lines from {name}");
				var lineQueue = new Queue<string?>();
				var lineReadHandler = new DataReceivedEventHandler((_, e) =>
				{
					lock (readingLock)
					{
						lineQueue.Enqueue(e.Data);
						Monitor.Pulse(readingLock);
					}
				});
				lineReadEvent.AddEventHandler(this.process, lineReadHandler);

				// read lines
				try
				{
					beginReadLineAction();
				}
				catch (Exception ex)
				{
					this.source.Logger.LogError(ex, $"Failed to start reading lines from {name}");
					lock (this.syncLock)
						Monitor.PulseAll(this.syncLock);
					return;
				}
				var isReadingFirstLine = true;
				while (true)
				{
					// wait for start reading line
					while (true)
					{
						lock (this.syncLock)
						{
							if (this.endOfStream)
								break;
							else if (!this.isReadingLine || bufferedLine != null)
								Monitor.Wait(this.syncLock);
							else
								break;
						}
					}
					if (this.endOfStream)
						break;

					// read line
					var line = (string?)null;
					try
					{
						if (isReadingFirstLine)
							this.source.Logger.LogTrace($"Wait for first line from {name}");
						lock (readingLock)
						{
							if (!lineQueue.TryDequeue(out line))
							{
								Monitor.Wait(readingLock);
								lineQueue.TryDequeue(out line);
							}
							if (isReadingFirstLine)
							{
								isReadingFirstLine = false;
								this.source.Logger.LogTrace($"First line read from {name}");
							}
						}
					}
					catch
					{ }

					// notify
					lock (this.syncLock)
					{
						bufferedLine = line;
						Monitor.PulseAll(this.syncLock);
					}
					if (line == null)
						break;
				}
				this.source.Logger.LogTrace($"Complete reading lines from {name}");
			}
		}


		// Constants.
		const uint SCS_32BIT_BINARY = 0; // 32-bit Windows application
		const uint SCS_64BIT_BINARY = 6; // 64-bit Windows application


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

			// check executable type
			var isSameArchExecutable = Global.Run(() =>
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
				{
					reader = null;
					return LogDataSourceState.SourceNotFound;
				}
			}
			catch (Win32Exception)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			reader = new ReaderImpl(this, process, options.IncludeStandardError);
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
