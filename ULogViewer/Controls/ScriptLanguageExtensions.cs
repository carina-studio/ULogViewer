using CarinaStudio.AppSuite.Scripting;
using System;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Extensions for <see cref="ScriptLanguage"/>.
/// </summary>
static class ScriptLanguageExtensions
{
    extension(ScriptLanguage language)
    {
        /// <summary>
        /// Get the version-independent base name of the script language used to compose documentation URIs.
        /// </summary>
        public string BaseName => language switch
        {
            ScriptLanguage.CSharp_13 or ScriptLanguage.CSharp_14 => "CSharp",
            ScriptLanguage.JavaScript_ES_5_1 => "JavaScript",
            ScriptLanguage.Python_3_4 => "Python",
            _ => throw new NotImplementedException(),
        };
    }
}
