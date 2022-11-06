using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Implementation of <see cref="ILogDataSource"/> based-on HTTP request and response.
	/// </summary>
	class HttpLogDataSource : BaseLogDataSource
	{
		// Implementation of reader.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly TextReader contentReader;
			readonly HttpClient httpClient;
			readonly HttpResponseMessage httpResponse;

			// Constructor.
			public ReaderImpl(HttpClient httpClient, HttpResponseMessage httpResponse, LogDataSourceOptions options)
			{
				this.httpClient = httpClient;
				this.httpResponse = httpResponse;
				var encoding = httpResponse.Content.Headers.ContentEncoding.Let(it =>
				{
					foreach (var encodingName in it)
					{
						try
						{
							return Encoding.GetEncoding(encodingName);
						}
						catch
						{ }
					}
					return null;
				}) ?? Encoding.UTF8;
				this.contentReader = new StreamReader(httpResponse.Content.ReadAsStream(), encoding);
				if (options.FormatJsonData)
					this.contentReader = new FormattedJsonTextReader(this.contentReader);
			}

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					this.contentReader.Dispose();
					this.httpResponse.Dispose();
					this.httpClient.Dispose();
				}
				base.Dispose(disposing);
			}

			// Implementations.
			public override int Peek() => this.contentReader.Peek();
			public override int Read() => this.contentReader.Read();
			public override int ReadBlock(char[] buffer, int offset, int count) => this.contentReader.ReadBlock(buffer, offset, count);
			public override string? ReadLine() => this.contentReader.ReadLine();
			public override string ReadToEnd() => this.contentReader.ReadToEnd();
		}


		/// <summary>
		/// Initialize new <see cref="HttpLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="options">Options.</param>
		public HttpLogDataSource(HttpLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
			if (options.Uri == null)
				throw new ArgumentException("No URI specified.");
			switch (options.Uri.Scheme)
			{
				case "http":
				case "https":
					break;
				default:
					throw new ArgumentException($"Invalid URI: {options.Uri}.");
			}
		}


		// Open reader.
		protected override async Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
		{
			// setup HTTP client
			var options = this.CreationOptions;
			var credential = (options.UserName != null || options.Password != null)
				? new NetworkCredential(options.UserName, options.Password)
				: null;
			var httpClient = credential == null
				? new HttpClient()
				: new HttpClient(new HttpClientHandler() { Credentials = credential }, true);

			// get response
			var uri = options.Uri.AsNonNull();
			var response = (HttpResponseMessage?)null;
			try
			{
#if DEBUG
				this.Logger.LogDebug("Start getting response from {uri}", uri);
#endif
				response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
#if DEBUG
				this.Logger.LogDebug("Complete getting response from {uri}", uri);
#endif
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					this.Logger.LogWarning(ex, "Getting response from {uri} has been cancelled", uri);
					return (LogDataSourceState.UnclassifiedError, null);
				}
				this.Logger.LogError(ex, "Failed to get response from {uri}", uri);
				if (ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound)
					return (LogDataSourceState.SourceNotFound, null);
				return (LogDataSourceState.UnclassifiedError, null);
			}

			// open reader
			return (LogDataSourceState.ReaderOpened, new ReaderImpl(httpClient, response, options));
		}


		// Prepare.
		protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken) => 
			Task.FromResult(LogDataSourceState.ReadyToOpenReader);
	}
}
