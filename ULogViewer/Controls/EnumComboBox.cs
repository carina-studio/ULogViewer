using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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
		IDisposable? itemsBinding;


		/// <summary>
		/// Get or set type of enumeration.
		/// </summary>
		public Type EnumType
		{
			get => this.GetValue<Type>(EnumTypeProperty);
			set => this.SetValue<Type>(EnumTypeProperty, value);
		}


		// Values of enumeration.
		Array? EnumValues { get; set; }


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == EnumTypeProperty)
			{
				this.EnumValues = null;
				this.enumConverter = null;
				this.itemsBinding = this.itemsBinding.DisposeAndReturnNull();
				if (change.NewValue.Value is Type type)
				{
					this.EnumValues = Enum.GetValues(type);
					this.enumConverter = new EnumConverter(App.Current, type);
					this.itemsBinding = this.Bind(ItemsProperty, new Binding()
					{
						Converter = this.enumConverter,
						Path = nameof(EnumValues),
						Source = this,
					});
				}
			}
		}


		// Interface implementations.
		Type IStyleable.StyleKey => typeof(ComboBox);
	}
}
