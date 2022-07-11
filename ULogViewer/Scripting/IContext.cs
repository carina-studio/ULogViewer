using ASControls = CarinaStudio.AppSuite.Controls;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Context for running script.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Get data for running script.
    /// </summary>
    IDictionary<string, object> Data { get; }


    /// <summary>
    /// Get logger.
    /// </summary>
    ILogger Logger { get; }


    /// <summary>
    /// Show message dialog.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="icon">Icon.</param>
    /// <param name="buttons">Buttons.</param>
    /// <returns>Result of dialog.</returns>
    MessageDialogResult ShowMessageDialog(string message, MessageDialogIcon icon = MessageDialogIcon.Information, MessageDialogButtons buttons = MessageDialogButtons.OK);
}


/// <summary>
/// Combonation of buttons of message dialog.
/// </summary>
public enum MessageDialogButtons
{
    OK = ASControls.MessageDialogButtons.OK,
    OKCancel = ASControls.MessageDialogButtons.OKCancel,
    YesNo = ASControls.MessageDialogButtons.YesNo,
    YesNoCancel = ASControls.MessageDialogButtons.YesNoCancel,
}


/// <summary>
/// Icon of message dialog.
/// </summary>
public enum MessageDialogIcon
{
    OK = ASControls.MessageDialogIcon.Error,
    Information = ASControls.MessageDialogIcon.Information,
    Question = ASControls.MessageDialogIcon.Question,
    Success = ASControls.MessageDialogIcon.Success,
    Warning = ASControls.MessageDialogIcon.Warning,
}


/// <summary>
/// Result of message dialog.
/// </summary>
public enum MessageDialogResult
{
    Cancel = ASControls.MessageDialogResult.Cancel,
    No = ASControls.MessageDialogResult.No,
    OK = ASControls.MessageDialogResult.OK,
    Yes = ASControls.MessageDialogResult.Yes,
}