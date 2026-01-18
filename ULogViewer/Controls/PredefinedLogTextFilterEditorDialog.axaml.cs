using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit or create <see cref="PredefinedLogTextFilter"/>.
/// </summary>
class PredefinedLogTextFilterEditorDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
{
	/// <summary>
	/// Key of persistent state tp indicate whether tutorial of group name has been shown or not.
	/// </summary>
	public static readonly SettingKey<bool> IsGroupNameTutorialShownKey = new("PredefinedLogTextFilterEditorDialog.IsGroupNameTutorialShown", true);
	
	
	// Static fields.
	static readonly Dictionary<PredefinedLogTextFilter, PredefinedLogTextFilterEditorDialog> DialogWithEditingRuleSets = new();


	// Fields.
	PredefinedLogTextFilter? editingFilter;
	readonly ToggleSwitch exclusiveModeSwitch;
	readonly ObservableList<MenuItem> groupMenuItems = new();
	readonly ContextMenu groupNameSelectionMenu;
	readonly TextBox groupNameTextBox;
	Regex? initialPattern;
	readonly TextBox nameTextBox;
	readonly PatternEditor patternEditor;
	readonly ToggleButton selectGroupNameButton;


	// Constructor.
	public PredefinedLogTextFilterEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.exclusiveModeSwitch = this.Get<ToggleSwitch>(nameof(exclusiveModeSwitch));
		this.groupNameSelectionMenu = ((ContextMenu)this.Resources[nameof(groupNameSelectionMenu)]!).Also(it =>
		{
			it.ItemsSource = this.groupMenuItems;
			it.Closed += (_, _) => this.selectGroupNameButton!.IsChecked = false;
			it.Opened += (_, _) => this.selectGroupNameButton!.IsChecked = true;
		});
		this.groupNameTextBox = this.Get<TextBox>(nameof(groupNameTextBox)).Also(it =>
		{
			it.LostFocus += (_, _) => it.Text = PredefinedLogTextFilter.CorrectGroupName(it.Text);
		});
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox));
		this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor));
		this.selectGroupNameButton = this.Get<ToggleButton>(nameof(selectGroupNameButton));
	}


	/// <summary>
	/// Complete editing.
	/// </summary>
	public async Task CompleteEditing()
	{
		// validate parameters
		if (string.IsNullOrWhiteSpace(this.nameTextBox.Text))
		{
			this.HintForInput(null, this.Get<Control>("nameItem"), this.nameTextBox);
			return;
		}
		if (this.patternEditor.Pattern is null)
		{
			this.HintForInput(null, this.Get<Control>("patternItem"), this.patternEditor);
			return;
		}
		
		// check group name
		var textFilterManager = PredefinedLogTextFilterManager.Default;
		var groupName = PredefinedLogTextFilter.CorrectGroupName(this.groupNameTextBox.Text);
		if (!string.IsNullOrEmpty(groupName)
		    && !this.Application.ProductManager.IsProductActivated(Products.Professional)
		    && textFilterManager.Groups.FirstOrDefault(it => it.Name == groupName) is null
		    && textFilterManager.Groups.Count >= 1)
		{
			await new MessageDialog
			{
				Icon = MessageDialogIcon.Information,
				Message = this.Application.GetObservableString("PredefinedLogTextFilterEditorDialog.GroupName.ProVersionOnly"),
			}.ShowDialog(this);
			this.groupNameTextBox.Focus();
			this.groupNameTextBox.SelectAll();
			return;
		}
		
		// edit or add filter
		var name = this.nameTextBox.Text.AsNonNull();
		var regex = this.patternEditor.Pattern.AsNonNull();
		var filter = this.editingFilter;
		if (filter != null)
		{
			filter.Name = name;
			filter.Regex = regex;
		}
		else
			filter = new PredefinedLogTextFilter(this.Application, name, regex);
		filter.GroupName = groupName;
		filter.Mode = this.exclusiveModeSwitch.IsChecked.GetValueOrDefault()
			? PredefinedLogTextFilterMode.Exclusion
			: PredefinedLogTextFilterMode.Inclusion;
		if (!textFilterManager.Filters.Contains(filter))
			textFilterManager.AddFilter(filter);

		// close window
		this.Close();
	}


	// Create menu item for given filter group.
	MenuItem CreateGroupMenuItem(PredefinedLogTextFilterGroup group) => new MenuItem().Also(it =>
	{
		it.Click += (_, _) =>
		{
			this.groupNameSelectionMenu.Close();
			this.SelectGroupName(group);
		};
		it.DataContext = group;
		it.Header = group.Name;
	});


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.editingFilter != null)
			DialogWithEditingRuleSets.Remove(this.editingFilter);
		if (PredefinedLogTextFilterManager.Default.Groups is INotifyCollectionChanged notifyCollectionChanged)
			notifyCollectionChanged.CollectionChanged -= this.OnFilterGroupsChanged;
		base.OnClosed(e);
	}


	// Called when list of filter groups changed.
	void OnFilterGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				e.NewItems!.Cast<PredefinedLogTextFilterGroup>().Let(groups =>
				{
					for (int i = 0, count = groups.Count; i < count; ++i)
						this.groupMenuItems.Insert(e.NewStartingIndex + i, this.CreateGroupMenuItem(groups[i]));
				});
				break;
			case NotifyCollectionChangedAction.Move:
				e.OldItems!.Cast<PredefinedLogTextFilterGroup>().Let(groups =>
				{
					if (groups.Count != 1)
						throw new NotSupportedException();
					var menuItem = this.groupMenuItems[e.OldStartingIndex];
					this.groupMenuItems.RemoveAt(e.OldStartingIndex);
					this.groupMenuItems.Insert(e.NewStartingIndex, menuItem);
				});
				break;
			case NotifyCollectionChangedAction.Remove:
				this.groupMenuItems.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
				break;
			case NotifyCollectionChangedAction.Replace:
				e.NewItems!.Cast<PredefinedLogTextFilterGroup>().Let(groups =>
				{
					for (int i = 0, count = groups.Count; i < count; ++i)
						this.groupMenuItems[e.NewStartingIndex + i] = this.CreateGroupMenuItem(groups[i]);
				});
				break;
			case NotifyCollectionChangedAction.Reset:
				this.groupMenuItems.Clear();
				foreach (var group in PredefinedLogTextFilterManager.Default.Groups)
					this.groupMenuItems.Add(this.CreateGroupMenuItem(group));
				break;
			default:
				throw new NotSupportedException();
		}
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() =>
		{
			var presenter = this.TutorialPresenter;
			if (presenter is not null)
			{
				if (!this.PersistentState.GetValueOrDefault(IsGroupNameTutorialShownKey))
				{
					this.PersistentState.SetValue(IsGroupNameTutorialShownKey, true);
					if (PredefinedLogTextFilterManager.Default.Groups.IsEmpty())
					{
						presenter.ShowTutorial(new Tutorial().Also(it =>
						{
							it.Anchor = this.FindControl<Control>("groupNameItemContainer");
							it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("PredefinedLogTextFilterEditorDialog.Tutorial.GroupName"));
							it.Dismissed += (_, _) =>
							{
								if (this.IsClosed)
									return;
								if (!this.patternEditor.ShowTutorialIfNeeded(presenter, this.nameTextBox))
									this.nameTextBox.Focus();
							};
							it.Icon = this.FindResourceOrDefault<IImage>("Image/Icon.Lightbulb.Colored.Gradient");
							it.IsSkippingAllTutorialsAllowed = false;
						}));
						return;
					}
				}
				if (this.patternEditor.ShowTutorialIfNeeded(presenter, this.nameTextBox))
					return;
			}
			this.nameTextBox.Focus();
		});
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		var filter = this.editingFilter;
		if (filter is null)
		{
			this.Bind(TitleProperty, this.Application.GetObservableString("PredefinedLogTextFilterEditorDialog.Title.Create"));
			this.patternEditor.Pattern = this.initialPattern;
		}
		else
		{
			this.Bind(TitleProperty, this.Application.GetObservableString("PredefinedLogTextFilterEditorDialog.Title.Edit"));
			this.exclusiveModeSwitch.IsChecked = filter.Mode == PredefinedLogTextFilterMode.Exclusion;
			this.groupNameTextBox.Text = PredefinedLogTextFilter.CorrectGroupName(filter.GroupName);
			this.nameTextBox.Text = filter.Name;
			this.patternEditor.Pattern = filter.Regex;
		}
		PredefinedLogTextFilterManager.Default.Groups.Let(groups =>
		{
			if (groups is INotifyCollectionChanged notifyCollectionChanged)
				notifyCollectionChanged.CollectionChanged += this.OnFilterGroupsChanged;
			foreach (var group in groups)
				this.groupMenuItems.Add(this.CreateGroupMenuItem(group));
		});
	}


	// Select group name.
	void SelectGroupName(PredefinedLogTextFilterGroup group)
	{
		this.groupNameTextBox.Text = group.Name;
		this.SynchronizationContext.Post(() =>
		{
			this.groupNameTextBox.Focus();
			this.groupNameTextBox.SelectAll();
		});
	}


	/// <summary>
	/// Show dialog to edit given text filter.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="filter">Text filter to edit.</param>
	/// <param name="regex">Preferred regex for text filter.</param>
	public static void Show(Avalonia.Controls.Window parent, PredefinedLogTextFilter? filter, Regex? regex)
	{
		// show existing dialog
		if (filter != null && DialogWithEditingRuleSets.TryGetValue(filter, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}

		// show dialog
		dialog = new()
		{
			editingFilter = filter,
			initialPattern = regex,
		};
		if (filter != null)
			DialogWithEditingRuleSets[filter] = dialog;
		dialog.Show(parent);
	}


	/// <summary>
	/// Show menu to select group name.
	/// </summary>
	public void ShowGroupNameSelectionMenu() =>
		this.groupNameSelectionMenu.Open(this.selectGroupNameButton);
}
