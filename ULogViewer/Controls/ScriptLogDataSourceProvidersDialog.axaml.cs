using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to manage <see cref="ScriptLogDataSourceProvider"/>s.
/// </summary>
partial class ScriptLogDataSourceProvidersDialog : CarinaStudio.Controls.Dialog<IULogViewerApplication>
{
	// Static fields.
	static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("ScriptLogDataSourceProvidersDialog.DonotShowRestrictionsWithNonProVersion");


	// Fields.
	readonly Avalonia.Controls.ListBox providerListBox;


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProvidersDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProvidersDialog()
	{
		this.CopyProviderCommand = new Command<ScriptLogDataSourceProvider>(this.CopyProvider);
		this.EditProviderCommand = new Command<ScriptLogDataSourceProvider>(this.EditProvider);
		this.ExportProviderCommand = new Command<ScriptLogDataSourceProvider>(this.ExportProvider);
		this.RemoveProviderCommand = new Command<ScriptLogDataSourceProvider>(this.RemoveProvider);
		AvaloniaXamlLoader.Load(this);
		this.providerListBox = this.Get<AppSuite.Controls.ListBox>(nameof(providerListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (_, e) => this.EditProvider((ScriptLogDataSourceProvider)e.Item);
		});
	}


	/// <summary>
	/// Add new provider.
	/// </summary>
	public async void AddProvider()
	{
		// check Pro version
		if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
			&& !LogDataSourceProviders.CanAddScriptProvider)
		{
			await new MessageDialog()
			{
				Icon = MessageDialogIcon.Warning,
				Message = this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.CannotAddMoreProviderWithoutProVersion"),
			}.ShowDialog(this);
			return;
		}

		// add provider
		var provider = await new ScriptLogDataSourceProviderEditorDialog().ShowDialog<ScriptLogDataSourceProvider?>(this);
		if (provider == null || !LogDataSourceProviders.AddScriptProvider(provider))
			return;
		this.providerListBox.SelectedItem = provider;
		this.providerListBox.Focus();
	}


	// Copy provider.
	async void CopyProvider(ScriptLogDataSourceProvider provider)
	{
		// check Pro version
		if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
			&& !LogDataSourceProviders.CanAddScriptProvider)
		{
			await new MessageDialog()
			{
				Icon = MessageDialogIcon.Warning,
				Message = this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.CannotAddMoreProviderWithoutProVersion"),
			}.ShowDialog(this);
			return;
		}

		// find new name for provider
		var newName = Utility.GenerateName(provider.DisplayName, name => 
			LogDataSourceProviders.ScriptProviders.FirstOrDefault(it => it.DisplayName == name) != null);

		// copy provider
		var newProvider = await new ScriptLogDataSourceProviderEditorDialog()
		{
			Provider = new ScriptLogDataSourceProvider(provider, newName),
		}.ShowDialog<ScriptLogDataSourceProvider?>(this);
		if (newProvider == null || !LogDataSourceProviders.AddScriptProvider(newProvider))
			return;
		this.providerListBox.SelectedItem = newProvider;
		this.providerListBox.Focus();
	}


	/// <summary>
	/// Command to copy provider.
	/// </summary>
	public ICommand CopyProviderCommand { get; }


	// Edit provider.
	async void EditProvider(ScriptLogDataSourceProvider provider)
	{
		await new ScriptLogDataSourceProviderEditorDialog()
		{
			Provider = provider,
		}.ShowDialog<ScriptLogDataSourceProvider?>(this);
		this.providerListBox.SelectedItem = null;
		this.providerListBox.SelectedItem = provider;
		this.providerListBox.Focus();
	}


	/// <summary>
	/// Command to edit provider.
	/// </summary>
	public ICommand EditProviderCommand { get; }


	// Export provider.
	async void ExportProvider(ScriptLogDataSourceProvider provider)
	{
		// select file
		var fileName = (await this.StorageProvider.SaveFilePickerAsync(new()
		{
			FileTypeChoices = new FilePickerFileType[]
			{
				new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
				{
					Patterns = new string[] { "*.json" }
				}
			}
		}))?.Let(it =>
		{
			return it.TryGetUri(out var uri) ? uri.LocalPath : null;
		});
		if (string.IsNullOrEmpty(fileName))
			return;
		
		// export
		try
		{
			await provider.SaveAsync(fileName);
		}
		catch (Exception ex)
		{
			this.Logger.LogError(ex, "Failed to export script log data source provider to '{fileName}'", fileName);
			_ = new MessageDialog()
			{
				Icon = MessageDialogIcon.Error,
				Message = new FormattedString().Also(it =>
				{
					it.Arg1 = provider.DisplayName;
					it.Arg2 = fileName;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.FailedToExportProvider"));
				})
			}.ShowDialog(this);
		}
	}


	/// <summary>
	/// Command to export provider.
	/// </summary>
	public ICommand ExportProviderCommand { get; }


	/// <summary>
	/// Import provider.
	/// </summary>
	public async void ImportProvider()
	{
		// check Pro version
		if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
			&& !LogDataSourceProviders.CanAddScriptProvider)
		{
			await new MessageDialog()
			{
				Icon = MessageDialogIcon.Warning,
				Message = this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.CannotAddMoreProviderWithoutProVersion"),
			}.ShowDialog(this);
			return;
		}

		// select file
		var fileName = (await this.StorageProvider.OpenFilePickerAsync(new()
		{
			FileTypeFilter = new FilePickerFileType[]
			{
				new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
				{
					Patterns = new string[] { "*.json" }
				}
			}
		}))?.Let(it =>
		{
			return it.Count == 1 && it[0].TryGetUri(out var uri)
				? uri.LocalPath : null;
		});
		if (string.IsNullOrEmpty(fileName))
			return;
		
		// load provider
		var provider = await Global.RunOrDefaultAsync(async () =>
		{
			return await ScriptLogDataSourceProvider.LoadAsync(this.Application, fileName);
		}, 
		ex =>
		{
			this.Logger.LogError(ex, "Failed to import script log data source provider from '{fileName}'", fileName);
			_ = new MessageDialog()
			{
				Icon = MessageDialogIcon.Error,
				Message = new FormattedString().Also(it =>
				{
					it.Arg1 = fileName;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.FailedToImportProvider"));
				})
			}.ShowDialog(this);
		});
		if (provider == null)
			return;
		
		// select new display name
		var newName = Utility.GenerateName(provider.DisplayName, name =>
			LogDataSourceProviders.ScriptProviders.FirstOrDefault(it => it.DisplayName == name) != null);
		
		// edit provider and import
		var newProvider = await new ScriptLogDataSourceProviderEditorDialog()
		{
			Provider = new ScriptLogDataSourceProvider(provider, newName),
		}.ShowDialog<ScriptLogDataSourceProvider?>(this);
		if (newProvider == null || !LogDataSourceProviders.AddScriptProvider(newProvider))
			return;
		this.providerListBox.SelectedItem = newProvider;
		this.providerListBox.Focus();
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		if (!this.Application.ProductManager.IsProductActivated(Products.Professional))
		{
			this.SynchronizationContext.PostDelayed(async () =>
			{
				if (this.IsOpened 
					&& !this.PersistentState.GetValueOrDefault(DonotShowRestrictionsWithNonProVersionKey))
				{
					var messageDialog = new MessageDialog()
					{
						DoNotAskOrShowAgain = false,
						Icon = MessageDialogIcon.Information,
						Message = this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.RestrictionsOfNonProVersion"),
					};
					await messageDialog.ShowDialog(this);
					if (messageDialog.DoNotAskOrShowAgain == true)
						this.PersistentState.SetValue<bool>(DonotShowRestrictionsWithNonProVersionKey, true);
				}
			}, 300);
		}
		this.SynchronizationContext.Post(this.providerListBox.Focus);
	}


	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/ScriptLogDataSource");
#pragma warning restore CA1822
	

	// Remove provider.
	async void RemoveProvider(ScriptLogDataSourceProvider provider)
	{
		var logProfileCount = LogProfileManager.Default.Profiles.Count(it => it.DataSourceProvider == provider);
		var result = await new MessageDialog()
		{
			Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
			Icon = AppSuite.Controls.MessageDialogIcon.Question,
			Message = logProfileCount > 0 
				? new FormattedString().Also(it =>
				{
					it.Arg1 = provider.DisplayName;
					it.Arg2 = logProfileCount;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.ConfirmDeletingProvider.WithLogProfiles"));
				})
				: new FormattedString().Also(it =>
				{
					it.Arg1 = provider.DisplayName;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.ConfirmDeletingProvider"));
				}),
		}.ShowDialog(this);
		if (result != AppSuite.Controls.MessageDialogResult.Yes)
			return;
		LogDataSourceProviders.RemoveScriptProvider(provider);
		provider.Dispose();
		this.providerListBox.SelectedItem = null;
		this.providerListBox.Focus();
	}


	/// <summary>
	/// Command to remove provider.
	/// </summary>
	public ICommand RemoveProviderCommand { get; }
}
