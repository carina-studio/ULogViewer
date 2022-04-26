using CarinaStudio.Threading;
using System;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// Category of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogCategory : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Static fields.
    static readonly long DefaultMemorySize = (4 * IntPtr.Size) // Appliation, name, Categorizer, PropertyChanged
        + 4; // isNameValid
    

    // Fields.
    bool isNameValid;
    string? name;


    /// <summary>
    /// Initializer new <see cref="DisplayableLogCategory"/> instance.
    /// </summary>
    /// <param name="categorizer"><see cref="IDisplayableLogCategorizer{T}>"/> which generate this category.</param>
    /// <param name="log"><see cref="DisplayableLog"/> which represents this category.</param>
    public DisplayableLogCategory(IDisplayableLogCategorizer<DisplayableLogCategory> categorizer, DisplayableLog? log) : base(categorizer.Application)
    {
        this.Categorizer = categorizer;
        this.Log = log;
    }


    /// <summary>
    /// Get <see cref="IDisplayableLogCategorizer{T}>"/> which generate this category.
    /// </summary>
    public IDisplayableLogCategorizer<DisplayableLogCategory> Categorizer { get; }


    /// <summary>
    /// Invalidate and update name of category.
    /// </summary>
    public void InvalidateName()
    {
        this.VerifyAccess();
        if (this.isNameValid)
        {
            var name = this.OnUpdateName();
            if (this.name != name)
            {
                this.name = name;
                this.OnPropertyChanged(nameof(Name));
            }
        }
    }


    /// <summary>
    /// Get <see cref="DisplayableLog"/> which represents this category.
    /// </summary>
    public DisplayableLog? Log { get; }


    /// <summary>
    /// Get memory usage of this category in bytes.
    /// </summary>
    public virtual long MemorySize { get => DefaultMemorySize; }


    /// <summary>
    /// Get name of category.
    /// </summary>
    public string? Name
    {
        get
        {
            if (!this.CheckAccess())
                return this.name;
            if (!this.isNameValid)
            {
                this.name = this.OnUpdateName();
                this.isNameValid = true;
            }
            return this.name;
        }
    }


    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    protected virtual void OnPropertyChanged(string propertyName) => 
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    

    /// <summary>
    /// Called to update name of category.
    /// </summary>
    /// <returns>Name of category.</returns>
    protected virtual string? OnUpdateName() => null;

    
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <inheritdoc/>
    public override string ToString() =>
        $"{this.GetType().Name}: {this.name}";
}