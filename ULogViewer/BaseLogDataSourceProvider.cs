using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataSourceProvider"/>.
	/// </summary>
	abstract class BaseLogDataSourceProvider : ILogDataSourceProvider
	{
		// Fields.
		string displayName;
		readonly ICollection<ILogDataSource> activeSources = new List<ILogDataSource>();


		/// <summary>
		/// Initialize new <see cref="BaseLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app"><see cref="App"/>.</param>
		protected BaseLogDataSourceProvider(App app)
		{
			app.VerifyAccess();
			this.Application = app;
			this.displayName = this.GetType().Name;
			this.Logger = app.LoggerFactory.CreateLogger(this.displayName);
		}


		/// <summary>
		/// Get <see cref="App"/> instance.
		/// </summary>
		public App Application { get; }



		/// <summary>
		/// Get or set name for displaying purpose.
		/// </summary>
		public string DisplayName
		{
			get => this.displayName;
			protected set
			{
				this.VerifyAccess();
				if (this.displayName == value)
					return;
				this.displayName = value;
				this.OnPropertyChanged(nameof(DisplayName));
			}
		}


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
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Called when created <see cref="ILogDataSource"/> has been disposed.
		/// </summary>
		/// <param name="source"></param>
		protected virtual void OnSourceDisposed(ILogDataSource source)
		{
			this.Logger.LogDebug($"Source {source} disposed, active source count: {this.activeSources.Count}");
			this.OnPropertyChanged(nameof(ActiveSourceCount));
		}


		// Create source.
		public ILogDataSource CreateSource(LogDataSourceOptions options)
		{
			this.VerifyAccess();
			if (!this.AllowMultipleSources && this.activeSources.Count == 1)
				throw new InvalidOperationException("Mutiple active sources is not allowed.");
			return this.CreateSourceCore(options).Also((it) =>
			{
				this.activeSources.Add(it);
				this.Logger.LogDebug($"Source {it} created, active source count: {this.activeSources.Count}");
				this.OnPropertyChanged(nameof(ActiveSourceCount));
			});
		}


		/// <summary>
		/// Create <see cref="ILogDataSource"/> instance.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns><see cref="ILogDataSource"/> instance.</returns>
		protected abstract ILogDataSource CreateSourceCore(LogDataSourceOptions options);


		// Interface implementations.
		public int ActiveSourceCount { get => this.activeSources.Count; }
		public virtual bool AllowMultipleSources => true;
		public bool CheckAccess() => this.Application.CheckAccess();
		IApplication IApplicationObject.Application { get => this.Application; }
		public abstract string Name { get; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
		public abstract UnderlyingLogDataSource UnderlyingSource { get; }
	}
}
