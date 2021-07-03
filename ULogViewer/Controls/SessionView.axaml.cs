using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	partial class SessionView : BaseView
	{
		// Fields.
		readonly MutableObservableBoolean canSetWorkingDirectory = new MutableObservableBoolean();
		readonly Grid logHeaderGrid;
		readonly ListBox logListBox;


		/// <summary>
		/// Initialize new <see cref="SessionView"/> instance.
		/// </summary>
		public SessionView()
		{
			// create commands
			this.SetWorkingDirectoryCommand = ReactiveCommand.Create(this.SetWorkingDirectory, this.canSetWorkingDirectory);

			// initialize
			this.InitializeComponent();

			// setup controls
			this.logHeaderGrid = this.FindControl<Grid>("logHeaderGrid").AsNonNull();
			this.logListBox = this.FindControl<ListBox>("logListBox").AsNonNull();
		}


		// Attach to session.
		void AttachToSession(Session session)
		{
			// add event handler
			session.PropertyChanged += this.OnSessionPropertyChanged;

			// attach to command
			session.SetWorkingDirectoryCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			this.canSetWorkingDirectory.Update(session.SetWorkingDirectoryCommand.CanExecute(null));

			// update UI
			this.OnDisplayLogPropertiesChanged();
		}


		// Detach from session.
		void DetachFromSession(Session session)
		{
			// remove event handler
			session.PropertyChanged -= this.OnSessionPropertyChanged;

			// detach from commands
			session.SetWorkingDirectoryCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			this.canSetWorkingDirectory.Update(false);

			// update UI
			this.OnDisplayLogPropertiesChanged();
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when display log properties changed.
		void OnDisplayLogPropertiesChanged()
		{
			// clear headers
			foreach (var control in this.logHeaderGrid.Children)
				control.DataContext = null;
			this.logHeaderGrid.Children.Clear();
			this.logHeaderGrid.ColumnDefinitions.Clear();

			// get display log properties
			var logProperties = (this.DataContext as Session)?.DisplayLogProperties;
			if (logProperties == null || logProperties.IsEmpty())
				return;

			// build headers
			var headerTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(DisplayableLogProperty));
			for (int i = 0, count = logProperties.Count; i < count; ++i)
			{
				// define column
				var logProperty = logProperties[i];
				var width = logProperty.Width;
				var columnWidth = width.Let(width =>
				{
					if (width == null)
						return new GridLength(1, GridUnitType.Star);
					return new GridLength(0, GridUnitType.Auto);
				});
				var column = new ColumnDefinition(columnWidth).Also(it =>
				{
					it.SharedSizeGroup = logProperty.Name;
				});
				this.logHeaderGrid.ColumnDefinitions.Add(column);

				// create header view
				var headerView = ((Border)headerTemplate.Build(logProperty)).Also(it =>
				{
					it.DataContext = logProperty;
					if (i == 0)
						it.BorderThickness = new Thickness();
					if (width == null)
						it.HorizontalAlignment = HorizontalAlignment.Stretch;
					else if (width > 0)
					{
						it.FindChildControl<TextBlock>("widthControlTextBlock").AsNonNull().Text = new string(new char[width.Value].Also(it =>
						{
							for (var j = it.Length - 1; j >= 0; --j)
								it[j] = ' ';
						}));
					}
					Grid.SetColumn(it, i);
				});
				this.logHeaderGrid.Children.Add(headerView);
			}
		}


		// Property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == DataContextProperty)
			{
				(change.OldValue.Value as Session)?.Let(session => this.DetachFromSession(session));
				(change.NewValue.Value as Session)?.Let(session => this.AttachToSession(session));
			}
		}


		// Called when CanExecute of command of Session has been changed.
		void OnSessionCommandCanExecuteChanged(object? sender, EventArgs e)
		{
			if (this.DataContext is not Session session)
				return;
			if (sender == session.SetWorkingDirectoryCommand)
			{
				if (session.SetWorkingDirectoryCommand.CanExecute(null))
				{
					this.canSetWorkingDirectory.Update(true);
					if (this.Application.Settings.GetValueOrDefault(Settings.SelectWorkingDirectoryWhenNeeded))
						this.SetWorkingDirectory();
				}
				else
					this.canSetWorkingDirectory.Update(false);
			}
		}


		// Called when property of session has been changed.
		void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Session.DisplayLogProperties):
					this.OnDisplayLogPropertiesChanged();
					break;
			}
		}


		// Set working directory.
		async void SetWorkingDirectory()
		{
			// check state
			if (!this.canSetWorkingDirectory.Value)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
			{
				this.Logger.LogError("Unable to set working directory without attaching to window");
				return;
			}

			// select directory
			var directory = await new OpenFolderDialog().ShowAsync(window);
			if (string.IsNullOrWhiteSpace(directory))
				return;

			// check state
			if (!this.canSetWorkingDirectory.Value)
				return;
			if (this.DataContext is not Session session)
				return;

			// set working directory
			session.SetWorkingDirectoryCommand.Execute(directory);
		}


		/// <summary>
		/// Command to set working directory.
		/// </summary>
		public ICommand SetWorkingDirectoryCommand { get; }
	}
}
