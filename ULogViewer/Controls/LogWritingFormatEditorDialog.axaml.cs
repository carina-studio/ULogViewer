using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

partial class LogWritingFormatEditorDialog : InputDialog
{
    // Fields.
    readonly TextBox formatTextBox;


    // Constructor.
    public LogWritingFormatEditorDialog()
    {
        AvaloniaXamlLoader.Load(this);
        this.formatTextBox = this.Get<StringInterpolationFormatTextBox>(nameof(formatTextBox)).Also(it =>
        {
            it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
            foreach (var propertyName in Logs.Log.PropertyNames)
            {
                it.PredefinedVariables.Add(new StringInterpolationVariable().Also(variable =>
                {
                    variable.Bind(StringInterpolationVariable.DisplayNameProperty, new Binding() 
                    {
                        Converter = Converters.LogPropertyNameConverter.Default,
                        Path = nameof(StringInterpolationVariable.Name),
                        Source = variable,
                    });
                    variable.Name = propertyName;
                }));
            }
            it.PredefinedVariables.Add(new StringInterpolationVariable().Also(variable =>
            {
                variable.Bind(StringInterpolationVariable.DisplayNameProperty,this.GetResourceObservable("String/Common.NewLine"));
                variable.Name = "NewLine";
            }));
        });
    }


    /// <summary>
    /// Get or set format to be edited.
    /// </summary>
    public string? Format { get; set; }


    // Generate result.
    protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
        Task.FromResult<object?>(this.formatTextBox.Text.AsNonNull());


    /// <inheritdoc/>
    protected override void OnEnterKeyClickedOnInputControl(IControl control)
    {
        base.OnEnterKeyClickedOnInputControl(control);
    }


    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!string.IsNullOrWhiteSpace(this.Format))
            this.formatTextBox.Text = this.Format;
        this.SynchronizationContext.Post(this.formatTextBox.Focus);
    }


    /// <inheritdoc/>
    protected override bool OnValidateInput() =>
        base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.formatTextBox.Text);
}
