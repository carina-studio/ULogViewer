using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Web;

namespace CarinaStudio.ULogViewer.Net;

abstract class SimpleSearchProvider : SearchProvider
{
    // Fields.
    readonly string baseUriString;
    readonly string keywordSeparator;


    // Constructor.
    public SimpleSearchProvider(IULogViewerApplication app, string id, string baseUriString, string keywordSeparator = "+") : base(app, id)
    { 
        this.baseUriString = baseUriString;
        this.keywordSeparator = keywordSeparator;
    }


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
        var uriBuffer = new StringBuilder(this.baseUriString);
        for (var i = 0; i < keywordCount; ++i)
        {
            var k = keywords[i];
            if (!string.IsNullOrWhiteSpace(k))
            {
                ++validKeywordCount;
                if (validKeywordCount > 1)
                    uriBuffer.Append(this.keywordSeparator);
                uriBuffer.Append(HttpUtility.UrlEncode(k));
            }
        }
        uri = validKeywordCount > 0 ? new(uriBuffer.ToString()) : null;
        return uri != null;
    }
}