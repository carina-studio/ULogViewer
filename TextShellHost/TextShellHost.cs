using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Host of text shell.
/// </summary>
public class TextShellHost : BaseApplicationObject, IDisposable
{
    // Native symbols.
#pragma warning disable SYSLIB1054
    [DllImport("TextShellHost", EntryPoint = "TextShellHost_GetOutputWindowSize")]
    static extern int GetOutputWindowSize(out ushort columns, out ushort rows);
    [DllImport("TextShellHost", EntryPoint = "TextShellHost_SetOutputWindowSize")]
    static extern int SetOutputWindowSize(ushort columns, ushort rows);
#pragma warning restore SYSLIB1054


    // Constants.
    const string ControlPipeStreamNameArg = "-control-pipe-name";
    const byte SetOutputWindowSizeCommand = 1;
    const string ShellExePathArg = "-shell";
    const string StartingMessageArg = "-message";
    const string OutputWindowHeightArg = "-output-height";
    const string OutputWindowWidthArg = "-output-width";


    // [Text shell host process]
    // Static fields.
    static string? ControlPipeStreamName;
    static ushort InitOutputWindowHeight;
    static ushort InitOutputWindowWidth;
    static volatile bool IsExiting;
    static string? ShellExePath;
    static string? StartingMessage;


    // Static fields.
    static int NextId = 1;
    static volatile ILogger? StaticLogger;


    // Fields.
    readonly NamedPipeServerStream controlPipeStream;
    readonly BinaryWriter controlPipeWriter;
    readonly Process hostProcess;
    readonly int id;
    int isDisposed;
    readonly ILogger logger;
    ushort outputWindowHeight;
    ushort outputWindowWidth;


    // Constructor.
    TextShellHost(IApplication app, Process hostProcess, NamedPipeServerStream controlPipeStream, ushort initWidth, ushort initHeight) : base(app)
    {
        // setup properties
        this.controlPipeStream = controlPipeStream;
        this.controlPipeWriter = new(controlPipeStream);
        this.hostProcess = hostProcess;
        this.id = Interlocked.Increment(ref NextId);
        this.logger = app.LoggerFactory.CreateLogger($"{nameof(TextShellHost)}-{this.id}");
        this.outputWindowWidth = initWidth;
        this.outputWindowHeight = initHeight;

        // attach to process
        this.ProcessId = hostProcess.Id;
        this.StandardError = hostProcess.StandardError;
        this.StandardInput = hostProcess.StandardInput;
        this.StandardOutput = hostProcess.StandardOutput;
        hostProcess.Exited += (_, e) =>
        {
            if (this.isDisposed != 0)
                return;
            var exitCode = this.hostProcess.ExitCode;
            if (exitCode == 0)
                this.logger.LogWarning("Host process {pid} has exited", this.hostProcess.Id);
            else
                this.logger.LogWarning("Host process {pid} has exited with code {code}", this.hostProcess.Id, exitCode);
            this.SynchronizationContext.Post(() => 
            {
                if (this.isDisposed != 0)
                    this.Exited?.Invoke(this, EventArgs.Empty);
            });
        };

        this.logger.LogDebug("Host process is {pid}", this.hostProcess.Id);
    }


    // Finalizer.
    ~TextShellHost() =>
        this.Dispose();


    // [Text shell host process]
    // Entry of control pipe client.
    static void ControlPipeClientStreamHandler(object? arg)
    {
        if (arg is not BinaryReader reader)
            return;
        while (true)
        {
            try
            {
                switch (reader.ReadByte())
                {
                    case SetOutputWindowSizeCommand:
                    {
                        var width = reader.ReadUInt16();
                        var height = reader.ReadUInt16();
                        UpdateOutputWindowSize(width, height);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsExiting)
                    break;
                Console.Error.WriteLine($"Unhandled error: {ex.GetType().Name}, {ex.Message}");
            }
        }
    }


    /// <summary>
    /// Create host instance asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="shellExePath">Path to executable of shell to be hosted.</param>
    /// <param name="startingMessage">Starting message.</param>
    /// <param name="initWidth">Initial width of output window in characters.</param>
    /// <param name="initHeight">Initial height of output window in characters.</param>
    /// <returns>Task of creating host instance.</returns>
    public static async Task<TextShellHost> CreateAsync(IApplication app, string shellExePath, string? startingMessage, ushort initWidth, ushort initHeight)
    {
        // check parameters
        if (string.IsNullOrWhiteSpace(shellExePath))
            throw new ArgumentException("Empty path to executable of shell.");
        if (initWidth == 0)
            throw new ArgumentOutOfRangeException(nameof(initWidth), "Width of output window cannot be 0.");
        if (initHeight == 0)
            throw new ArgumentOutOfRangeException(nameof(initHeight), "Height of output window cannot be 0.");
        
        // create logger
        StaticLogger ??= app.LoggerFactory.CreateLogger(nameof(TextShellHost));
        
        // create control stream
        StaticLogger.LogDebug("Create control stream for host process");
        string controlStreamName = $"ULV-{app.RootPrivateDirectoryPath.GetHashCode()}-{DateTime.UtcNow.ToBinary()}";
        var controlStream = new NamedPipeServerStream(controlStreamName);
        var waitForHostProcessTask = controlStream.WaitForConnectionAsync();

        // start host process
        var hostProcess = await Task.Run(() =>
        {
            StaticLogger.LogDebug("Start host process");
            return Process.Start(new ProcessStartInfo()
            {
                Arguments = $"{ShellExePathArg} \"{shellExePath}\" {StartingMessageArg} \"{startingMessage}\" {OutputWindowWidthArg} {initWidth} {OutputWindowHeightArg} {initHeight} {ControlPipeStreamNameArg} \"{controlStreamName}\"",
                CreateNoWindow = true,
                FileName = Path.Combine(app.RootPrivateDirectoryPath, "TextShellHost"),
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
        });
        if (hostProcess == null)
        {
            Global.RunWithoutErrorAsync(controlStream.Close);
            StaticLogger.LogError("Unable to start host process");
            throw new ArgumentException($"Unable to start host process.");
        }
        StaticLogger.LogDebug("Host process {pid} started", hostProcess.Id);

        // wait for connection of host process and complete
        try
        {
            StaticLogger.LogDebug("Wait for connection from host process");
            await waitForHostProcessTask.WaitAsync(TimeSpan.FromSeconds(3));
            return new(app, hostProcess, controlStream, initWidth, initHeight);
        }
        catch
        {
            Global.RunWithoutErrorAsync(controlStream.Close);
            Global.RunWithoutErrorAsync(hostProcess.Kill);
            throw;
        }
    }


    /// <summary>
    /// Dispose the host instance.
    /// </summary>
    public void Dispose()
    {
        // update state
        if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            return;
        GC.SuppressFinalize(this);

        // close control stream and kill the host process
        Task.Run(() =>
        {
            Global.RunWithoutError(this.controlPipeStream.Close);
            if (!this.hostProcess.HasExited)
            {
                this.logger.LogWarning("Kill host process {pid}", this.hostProcess.Id);
                Global.RunWithoutError(this.hostProcess.Kill);
            }
        });
    }


    /// <summary>
    /// Check whether host process has exited or not.
    /// </summary>
    public bool HasExited { get => this.hostProcess.HasExited; }


    /// <summary>
    /// Raised when host process has exited.
    /// </summary>
    public event EventHandler? Exited;


    // [Text shell host process]
    // Process entry point.
    static unsafe int Main(string[] args)
    {
        // get initial window size
        if (Platform.IsWindows)
        {
            InitOutputWindowWidth = (ushort)Console.WindowWidth;
            InitOutputWindowWidth = (ushort)Console.WindowHeight;
        }
        else
        {
            var result = GetOutputWindowSize(out InitOutputWindowWidth, out InitOutputWindowHeight);
            if (result != 0)
                Console.Error.WriteLine($"Unable to get info of output window: {Marshal.GetPInvokeErrorMessage(result)}.");
        }

        // parse args
        if (!ParseArgs(args))
            return 1;
        
        // connect to control server
        var controlStream = default(NamedPipeClientStream);
        if (!string.IsNullOrWhiteSpace(ControlPipeStreamName))
        {
            var controlStreamReader = default(BinaryReader);
            try
            {
                controlStream = new NamedPipeClientStream(ControlPipeStreamName);
                controlStream.Connect(5000);
                controlStreamReader = new(controlStream);
            }
            catch
            {
                Console.Error.WriteLine("Unable to connect to control server.");
                return 1;
            }
            new Thread(ControlPipeClientStreamHandler)
            {
                IsBackground = true,
                Name = "Control Client Handler",
            }.Start(controlStreamReader);
        }

        // setup initial output window size
        UpdateOutputWindowSize(InitOutputWindowWidth, InitOutputWindowHeight);
        
        // show starting message
        if (!string.IsNullOrWhiteSpace(StartingMessage))
            Console.WriteLine(StartingMessage);

        // start shell
        var process = default(Process);
        try
        {
            process = Process.Start(new ProcessStartInfo()
            {
                CreateNoWindow = true,
                FileName = ShellExePath!,
                UseShellExecute = false,
            });
            if (process == null)
            {
                Console.Error.WriteLine($"Unable to start shell '{ShellExePath}'.");
                return 1;
            }
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex.GetType().Name}, {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
        finally
        {
            IsExiting = true;
            if (process != null)
            {
                if (!process.HasExited)
                    Global.RunWithoutError(process.Kill);
                process.Dispose();
            }
            if (controlStream != null)
                Global.RunWithoutError(controlStream.Close);
        }
        return 1;
    }


    /// <summary>
    /// Get or set size of output window in characters.
    /// </summary>
    public (ushort, ushort) OutputWindowSize
    {
        get => (this.outputWindowWidth, this.outputWindowHeight);
        set
        {
            if (value.Item1 == 0 || value.Item2 == 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Width or height cannot be 0.");
            if (this.isDisposed != 0)
                throw new ObjectDisposedException($"{nameof(TextShellHost)}-{this.id}");
            this.outputWindowWidth = value.Item1;
            this.outputWindowHeight = value.Item2;
            Task.Run(() =>
            {
                try
                {
                    lock (this.controlPipeWriter)
                    {
                        this.controlPipeWriter.Write(SetOutputWindowSizeCommand);
                        this.controlPipeWriter.Write(value.Item1);
                        this.controlPipeWriter.Write(value.Item2);
                    }
                }
                catch (Exception ex)
                {
                    if (this.isDisposed != 0)
                        this.logger.LogError(ex, "Unable to set output window size to {w}x{h}", value.Item1, value.Item2);
                }
            });
        }
    }


    // [Text shell host process]
    // Parse arguments.
    static bool ParseArgs(string[] args)
    {
        var argCount = args.Length;
        for (var i = 0; i < argCount; ++i)
        {
            switch (args[i])
            {
                case ControlPipeStreamNameArg:
                    if (i >= argCount - 1)
                    {
                        Console.Error.WriteLine("No control pipe specified.");
                        return false;
                    }
                    ControlPipeStreamName = args[++i];
                    break;
                case ShellExePathArg:
                    if (i >= argCount - 1)
                    {
                        Console.Error.WriteLine("No shell specified.");
                        return false;
                    }
                    ShellExePath = args[++i];
                    break;
                case StartingMessageArg:
                    if (i < argCount - 1)
                        StartingMessage = args[++i];
                    break;
                case OutputWindowHeightArg:
                    if (i >= argCount - 1)
                    {
                        Console.Error.WriteLine("No output window height specified.");
                        return false;
                    }
                    if (!ushort.TryParse(args[++i], out InitOutputWindowHeight))
                    {
                        Console.Error.WriteLine($"Invalid output window height: {args[i]}.");
                        return false;
                    }
                    break;
                case OutputWindowWidthArg:
                    if (i >= argCount - 1)
                    {
                        Console.Error.WriteLine("No output window width specified.");
                        return false;
                    }
                    if (!ushort.TryParse(args[++i], out InitOutputWindowWidth))
                    {
                        Console.Error.WriteLine($"Invalid output window width: {args[i]}.");
                        return false;
                    }
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: '{args[i]}'.");
                    return false;
            }
        }
        if (string.IsNullOrWhiteSpace(ShellExePath))
        {
            Console.Error.WriteLine("No shell specified.");
            return false;
        }
        return true;
    }


    /// <summary>
    /// Get ID of host process.
    /// </summary>
    public int ProcessId { get; }


    /// <summary>
    /// Get reader of stderr of host process.
    /// </summary>
    public TextReader StandardError { get; }


    /// <summary>
    /// Get writer of stdin of host process.
    /// </summary>
    public TextWriter StandardInput { get; }


    /// <summary>
    /// Get reader of stdout of host process.
    /// </summary>
    public TextReader StandardOutput { get; }


    // [Text shell host process]
    // Update console window size.
    static unsafe void UpdateOutputWindowSize(ushort width, ushort height)
    {
        if (width < 1)
            width = 1;
        if (height < 1)
            height = 1;
        if (Platform.IsWindows)
        {
#pragma warning disable CA1416
            Console.SetWindowSize(width, height);
#pragma warning restore CA1416
        }
        else
        {
            var result = SetOutputWindowSize(width, height);
            if (result != 0)
                Console.Error.WriteLine($"Unable to set window size to {width}x{height}: {Marshal.GetPInvokeErrorMessage(result)}.");
        }
    }
}