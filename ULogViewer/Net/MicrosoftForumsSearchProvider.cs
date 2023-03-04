using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Web;

namespace CarinaStudio.ULogViewer.Net;

class MicrosoftForumsSearchProvider : SearchProvider
{
    // Constructor.
    public MicrosoftForumsSearchProvider(IULogViewerApplication app) : base(app, "MicrosoftForums")
    { }


    /// <inheritdoc/>
    public override bool TryCreateSearchUri(IList<string> keywords, [NotNullWhen(true)] out Uri? uri)
    {
        var keywordCount = keywords.Count;
        if (keywordCount == 0)
        {
            uri = null;
            return false;
        }
        var validKeywordCount = 0;
        var uriBuffer = new StringBuilder("https://social.microsoft.com/Forums/");
        uriBuffer.Append(this.Application.CultureInfo.Name);
        uriBuffer.Append("/home?category=&forum=&filter=&sort=relevancedesc&brandIgnore=true&searchTerm=");
        for (var i = 0; i < keywordCount; ++i)
        {
            var k = keywords[i];
            if (!string.IsNullOrWhiteSpace(k))
            {
                ++validKeywordCount;
                if (validKeywordCount > 1)
                    uriBuffer.Append('+');
                uriBuffer.Append(HttpUtility.UrlEncode(k));
            }
        }
        uri = validKeywordCount > 0 ? new(uriBuffer.ToString()) : null;
        return uri != null;
    }
}