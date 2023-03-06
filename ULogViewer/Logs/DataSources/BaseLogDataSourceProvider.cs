using CarinaStudio.AppSuite;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataSourceProvider"/>.
	/// </summary>
	abstract class BaseLogDataSourceProvider : ILogDataSourceProvider
	{
		// Fields.
		readonly ICollection<ILogDataSource> activeSources = new List<ILogDataSource>();
		readonly IDisposable appStringsUpdatedHandlerToken;
		string displayName = "";
		int isDisposed;


		/// <summary>
		/// Initialize new <see cref="BaseLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app"><see cref="IULogViewerApplication"/>.</param>
		protected BaseLogDataSourceProvider(IULogViewerApplication app)
		{
			app.VerifyAccess();
			this.Application = app;
			this.appStringsUpdatedHandlerToken = app.AddWeakEventHandler(nameof(IApplication.StringsUpdated), (_, _) =>
				this.DisplayName = this.OnUpdateDisplayName());
			this.Logger = app.LoggerFactory.CreateLogger(this.GetType().Name);
			this.SynchronizationContext.Post(() => this.DisplayName = this.OnUpdateDisplayName());
		}


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


		/// <summary>
		/// Get name for displaying purpose.
		/// </summary>
		public string DisplayName
		{
			get => this.displayName;
			private set
			{
				this.VerifyAccess();
				if (this.displayName == value)
					return;
				this.displayName = value;
				this.OnPropertyChanged(nameof(DisplayName));
			}
		}


		/// <inheritdoc/>
		public virtual IEnumerable<ExternalDependency> ExternalDependencies { get; } = Array.Empty<ExternalDependency>();


		/// <inheritdoc/>
		public virtual Uri? GetSourceOptionReferenceUri(string name) => null;


		/// <inheritdoc/>
		public virtual bool IsProVersionOnly { get => false; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		// Called when source has been disposed.
		internal void NotifySourceDisposedInternal(ILogDataSource source)
		{
			if (!this.activeSources.Remove(source))
				return;
			this.OnSourceDisposed(source);
		}


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.isDisposed == 0)
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}


		/// <summary>
		/// Called when created <see cref="ILogDataSource"/> has been disposed.
		/// </summary>
		/// <param name="source"></param>
		protected virtual void OnSourceDisposed(ILogDataSource source)
		{
			this.Logger.LogDebug("Source {source} disposed, active source count: {count}", source, this.activeSources.Count);
			this.OnPropertyChanged(nameof(ActiveSourceCount));
		}


		/// <summary>
		/// Called to update <see cref="DisplayName"/>.
		/// </summary>
		/// <returns>Display name.</returns>
		protected virtual string OnUpdateDisplayName() =>
			this.Application.GetStringNonNull($"{this.GetType().Name}.DisplayName", this.GetType().Name);


		// Create source.
		public ILogDataSource CreateSource(LogDataSourceOptions options)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.AllowMultipleSources && this.activeSources.Count == 1)
				throw new InvalidOperationException("Mutiple active sources is not allowed.");
			return this.CreateSourceCore(options).Also((it) =>
			{
				this.activeSources.Add(it);
				this.Logger.LogDebug("Source {it} created, active source count: {count}", it, this.activeSources.Count);
				this.OnPropertyChanged(nameof(ActiveSourceCount));
			});
		}


		/// <summary>
		/// Create <see cref="ILogDataSource"/> instance.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns><see cref="ILogDataSource"/> instance.</returns>
		protected abstract ILogDataSource CreateSourceCore(LogDataSourceOptions options);


		/// <summary>
		/// Dispose the provider.
		/// </summary>
		/// <param name="disposing">True to release managed resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
				return;
			if (disposing)
			{
				this.VerifyAccess();
				this.appStringsUpdatedHandlerToken.Dispose();
#pragma warning disable CA1816
				GC.SuppressFinalize(this);
#pragma warning restore CA1816
			}
		}


		/// <summary>
		/// Check whether the provider has been disposed or not.
		/// </summary>
		protected bool IsDisposed => this.isDisposed != 0;


		/// <summary>
		/// Get the set of name of options which are required by creating <see cref="ILogDataSource"/>.
		/// </summary>
		public abstract ISet<string> RequiredSourceOptions { get; }


		/// <summary>
		/// Get the set of name of options which are supported by this provider and created <see cref="ILogDataSource"/>.
		/// </summary>
		public abstract ISet<string> SupportedSourceOptions { get; }


		/// <summary>
		/// Validate whether given options are valid for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="options">Options to check.</param>
		/// <returns>True if options are valid for creating <see cref="ILogDataSource"/>.</returns>
		public virtual bool ValidateSourceOptions(LogDataSourceOptions options)
		{
			foreach (var optionName in this.RequiredSourceOptions)
			{
				if (!options.IsOptionSet(optionName))
					return false;
			}
			return true;
		}


		/// <summary>
		/// Throw <see cref="ObjectDisposedException"/> if the provider has been disposed.
		/// </summary>
		protected void VerifyDisposed()
		{
			if (this.isDisposed != 0)
				throw new ObjectDisposedException(this.GetType().Name);
		}


		// Interface implementations.
		public int ActiveSourceCount { get => this.activeSources.Count; }
		public virtual bool AllowMultipleSources => true;
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public abstract string Name { get; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
	}
}
