using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

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
			public ReaderImpl(HttpClient httpClient, HttpResponseMessage httpResponse)
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
			public override string? ReadLine() => this.contentReader.ReadLine();
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
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			// setup HTTP client
			var httpClient = this.CreationOptions.WebRequestCredentials.Let(it =>
			{
				if (it == null)
					return new HttpClient();
				return new HttpClient(new HttpClientHandler() { Credentials = it }, true);
			});

			// get response
			var uri = this.CreationOptions.Uri.AsNonNull();
			var response = (HttpResponseMessage?)null;
			try
			{
#if DEBUG
				this.Logger.LogDebug($"Start getting response from {uri}");
#endif
				var task = httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				task.Wait(cancellationToken);
#if DEBUG
				this.Logger.LogDebug($"Complete getting response from {uri}");
#endif
				response = task.Result;
			}
			catch (Exception ex)
			{
				reader = null;
				if (cancellationToken.IsCancellationRequested)
				{
					this.Logger.LogWarning(ex, $"Getting response from {uri} has been cancelled");
					return LogDataSourceState.UnclassifiedError;
				}
				this.Logger.LogError(ex, $"Failed to get response from {uri}");
				if (ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound)
					return LogDataSourceState.SourceNotFound;
				return LogDataSourceState.UnclassifiedError;
			}

			// open reader
			reader = new ReaderImpl(httpClient, response);
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore() => LogDataSourceState.ReadyToOpenReader;
	}
}
