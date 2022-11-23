using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Viewer of text shell.
/// </summary>
partial class TextShellView : CarinaStudio.Controls.UserControl<IULogViewerApplication>
{
	/// <summary>
	/// Property of <see cref="IsStartingTextShell"/>
	/// </summary>
	public static readonly DirectProperty<TextShellView, bool> IsStartingTextShellProperty = AvaloniaProperty.RegisterDirect<TextShellView, bool>(nameof(IsStartingTextShell), v => v.isStartingTextShell);


	// Constants.
	const int ExitTextShellDelay = 3000;


	// Fields.
	readonly ScheduledAction exitTextShellAction;
	bool isExitingTextShellRequested;
	bool isStartingTextShell;
	TextShell shell = SettingKeys.DefaultTextShell.DefaultValue;
	TextShellHost? shellHost;
	readonly StringBuilder shellOutputBuffer = new();
	readonly List<int> shellOutputLineLength = new();
	readonly ScheduledAction startTextShellAction;
	readonly TextBox textBox;


	/// <summary>
	/// Initialize new <see cref="TextShellView"/> instance.
	/// </summary>
	public TextShellView()
	{
		this.shell = this.Application.Settings.GetValueOrDefault(SettingKeys.DefaultTextShell);
		AvaloniaXamlLoader.Load(this);
		this.exitTextShellAction = new(() => _ = this.ExitTextShellAsync());
		this.startTextShellAction = new(() => _ = this.StartTextShellAsync());
		this.textBox = this.Get<TextBox>("PART_TextBox").Also(it =>
		{
			//
		});
	}


	/// <summary>
	/// Force exiting text shell asynchronously.
	/// </summary>
	/// <returns>Task of exiting text shell.</returns>
	public Task ExitTextShellAsync()
	{
		this.VerifyAccess();
		if (this.isStartingTextShell)
		{
			this.Logger.LogWarning("Exit text shell after starting completed");
			this.isExitingTextShellRequested = true;
			return Task.CompletedTask;
		}
		if (this.shellHost == null)
			return Task.CompletedTask;
		this.Logger.LogWarning("Start exiting text shell ({pid})", this.shellHost.ProcessId);
		this.exitTextShellAction.Cancel();
		this.isExitingTextShellRequested = false;
		this.shellHost.Dispose();
		this.OnTextShellExited();
		return Task.CompletedTask;
	}


	/// <summary>
	/// Check whether text shell is starting or not.
	/// </summary>
	public bool IsStartingTextShell { get => this.isStartingTextShell; }


	/// <inheritdoc/>
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		this.exitTextShellAction.Cancel();
		this.startTextShellAction.Schedule();
	}


	/// <inheritdoc/>
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		this.startTextShellAction.Cancel();
		this.exitTextShellAction.Schedule(ExitTextShellDelay);
		base.OnDetachedFromVisualTree(e);
	}


	// Called when text shell has exited.
	void OnTextShellExited(object? sender, EventArgs e) =>
		this.OnTextShellExited();
	void OnTextShellExited()
	{
		if (this.shellHost == null)
			return;
		var pid = this.shellHost.ProcessId;
		this.shellHost.Exited -= this.OnTextShellExited;
		this.shellHost = this.shellHost.DisposeAndReturnNull();
		this.Logger.LogWarning("Text shell ({pid}) exited", pid);
		this.TextShellExited?.Invoke(this, EventArgs.Empty);
	}


	// Read output from text shell continuously.
	async void ReadOutputFromTextShell(TextShellHost shellHost, TextReader reader)
	{
		var buffer = new char[1];
		while (true)
		{
			try
			{
				// read
				var readCount = await reader.ReadAsync(buffer, 0, buffer.Length);
				if (readCount == 0)
				{
					if (shellHost.HasExited)
						break;
					continue;
				}

				// add to buffer
				var c = buffer[0];
				switch (c)
				{
					case '\n':
						this.shellOutputLineLength.Add(0);
						this.shellOutputBuffer.AppendLine();
						break;
					case '\r':
						continue;
					default:
						if (this.shellOutputLineLength.IsNotEmpty())
							++this.shellOutputLineLength[^1];
						else
							this.shellOutputLineLength.Add(1);
						this.shellOutputBuffer.Append(c);
						break;
				}

				// show on view
				this.textBox.Text = this.shellOutputBuffer.ToString();
				this.textBox.SelectionEnd = this.shellOutputBuffer.Length;
				this.textBox.SelectionStart = this.shellOutputBuffer.Length;
			}
			catch (Exception ex)
			{
				if (this.shellHost != shellHost || shellHost.HasExited)
					break;
				this.Logger.LogError(ex, "Error occurred while reading from text shell");
			}
		}
	}


	/// <summary>
	/// Start text shell asynchronously.
	/// </summary>
	/// <returns>Task of starting text shell.</returns>
	public async Task<bool> StartTextShellAsync()
	{
		// check state
		this.VerifyAccess();
		if (this.shellHost != null || this.isStartingTextShell)
			return true;
		if (this.isExitingTextShellRequested)
		{
			this.Logger.LogError("Cannot start text shell because exiting has been requested");
			return false;
		}

		// cancel scheduled starting
		this.startTextShellAction.Cancel();
		
		// update state
		this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, true);
		if (this.isExitingTextShellRequested)
		{
			this.Logger.LogWarning("Exiting has been requested when starting text shell");
			this.isExitingTextShellRequested = false;
			this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, false);
			return false;
		}

		// get info of text shell
		if (!TextShellManager.Default.TryGetDefaultTextShellPath(out var shell, out var shellExePath))
		{
			this.Logger.LogError("Unable to get information of default text shell");
			this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, false);
			return false;
		}

		// start text shell
		var shellHost = default(TextShellHost);
		try
		{
			shellHost = await TextShellHost.CreateAsync(this.Application, shellExePath, null, 80, 25);
			this.Logger.LogWarning("Text shell ({pid}) started", shellHost.ProcessId);
		}
		catch (Exception ex)
		{
			this.Logger.LogError(ex, "Unable to start text shell {shell}", shell);
			this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, false);
			return false;
		}

		// exit shell if needed
		if (this.isExitingTextShellRequested)
		{
			this.Logger.LogWarning("Exiting has been requested when starting text shell");
			this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, false);
			await this.ExitTextShellAsync();
			return false;
		}

		// complete
		shellHost.Exited += this.OnTextShellExited;
		this.shellHost = shellHost;
		this.ReadOutputFromTextShell(shellHost, shellHost.StandardOutput);
		this.ReadOutputFromTextShell(shellHost, shellHost.StandardError);
		this.SetAndRaise(IsStartingTextShellProperty, ref this.isStartingTextShell, false);
		return true;
	}


	/// <summary>
	/// Raised when text shell has exited.
	/// </summary>
	public event EventHandler? TextShellExited;
}
