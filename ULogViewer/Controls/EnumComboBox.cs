using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;
using CarinaStudio.ULogViewer.Converters;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// <see cref="ComboBox"/> to show enumeration values.
	/// </summary>
	class EnumComboBox : ComboBox, IStyleable
	{
		/// <summary>
		/// Property of <see cref="EnumType"/>.
		/// </summary>
		public static readonly AvaloniaProperty<Type> EnumTypeProperty = AvaloniaProperty.Register<EnumComboBox, Type>(nameof(EnumType), validate: it => it == null || it.IsEnum);


		// Fields.
		EnumConverter? enumConverter;
		Array? enumValues;


		/// <summary>
		/// Get or set type of enumeration.
		/// </summary>
		public Type EnumType
		{
			get => this.GetValue<Type>(EnumTypeProperty);
			set => this.SetValue<Type>(EnumTypeProperty, value);
		}


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == EnumTypeProperty)
			{
				this.enumConverter = null;
				this.enumValues = null;
				this.Items = null;
				this.ItemTemplate = null;
				if (change.NewValue.Value is Type type)
				{
					this.enumConverter = new EnumConverter(App.Current, type);
					this.enumValues = Enum.GetValues(type);
					this.Items = this.enumValues;
					this.ItemTemplate = new DataTemplate()
					{
						Content = new Func<IServiceProvider, object>(_ =>
						{
							var textBlock = new TextBlock().Also(it =>
							{
								it.Bind(TextBlock.TextProperty, new Binding { Converter = this.enumConverter });
								it.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
							});
							return new ControlTemplateResult(textBlock, null);
						}),
						DataType = type,
					};
				}
			}
		}


		// Interface implementations.
		Type IStyleable.StyleKey => typeof(ComboBox);
	}
}
