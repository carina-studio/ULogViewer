using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to manage <see cref="ScriptLogDataSourceProvider"/>s.
/// </summary>
partial class ScriptLogDataSourceProvidersDialog : CarinaStudio.Controls.Dialog<IULogViewerApplication>
{
	// Static fields.
	static readonly Regex BaseNameRegex = new("^(?<Name>.+)\\s+\\(\\d+\\)\\s*$");


	// Fields.
	readonly Avalonia.Controls.ListBox providerListBox;


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProvidersDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProvidersDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.providerListBox = this.Get<AppSuite.Controls.ListBox>(nameof(providerListBox)).Also(it =>
		{
			it.DoubleClickOnItem += (_, e) => this.EditProvider((ScriptLogDataSourceProvider)e.Item);
		});
	}


	// Add new provider.
	async void AddProvider()
	{
		var provider = await new ScriptLogDataSourceProviderEditorDialog().ShowDialog<ScriptLogDataSourceProvider?>(this);
		if (provider == null || !LogDataSourceProviders.AddScriptProvider(provider))
			return;
		this.providerListBox.SelectedItem = provider;
		this.providerListBox.Focus();
	}


	// Copy provider.
	async void CopyProvider(ScriptLogDataSourceProvider provider)
	{
		var baseName = BaseNameRegex.Match(provider.DisplayName ?? "").Let(it =>
			it.Success ? it.Groups["Name"].Value : provider.DisplayName ?? "");
		var newName = baseName;
		for (var n = 2; n <= 10; ++n)
		{
			var candidateName = $"{baseName} ({n})";
			if (LogDataSourceProviders.ScriptProviders.FirstOrDefault(it => it.DisplayName == candidateName) == null)
			{
				newName = candidateName;
				break;
			}
		}
		var newProvider = await new ScriptLogDataSourceProviderEditorDialog()
		{
			Provider = new ScriptLogDataSourceProvider(provider, newName),
		}.ShowDialog<ScriptLogDataSourceProvider?>(this);
		if (newProvider == null || !LogDataSourceProviders.AddScriptProvider(newProvider))
			return;
		this.providerListBox.SelectedItem = newProvider;
		this.providerListBox.Focus();
	}


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


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(this.providerListBox.Focus);
	}


	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/Scripting");
	

	// Remove provider.
	async void RemoveProvider(ScriptLogDataSourceProvider provider)
	{
		var logProfileCount = LogProfileManager.Default.Profiles.Count(it => it.DataSourceProvider == provider);
		var result = await new MessageDialog()
		{
			Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
			Icon = AppSuite.Controls.MessageDialogIcon.Question,
			Message = logProfileCount > 0 
				? this.Application.GetFormattedString("ScriptLogDataSourceProvidersDialog.ConfirmDeletingProvider.WithLogProfiles", provider.DisplayName, logProfileCount)
				: this.Application.GetFormattedString("ScriptLogDataSourceProvidersDialog.ConfirmDeletingProvider", provider.DisplayName),
		}.ShowDialog(this);
		if (result != AppSuite.Controls.MessageDialogResult.Yes)
			return;
		LogDataSourceProviders.RemoveScriptProvider(provider);
		this.providerListBox.SelectedItem = null;
		this.providerListBox.Focus();
	}
}
