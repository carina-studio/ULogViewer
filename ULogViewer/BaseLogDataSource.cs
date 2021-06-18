using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Base implementation of <see cref="ILogDataSource"/>.
	/// </summary>
	abstract class BaseLogDataSource : BaseDisposable, ILogDataSource
	{
		// Static fields.
		static int nextId = 1;


		// Fields.
		string? displayName;


		/// <summary>
		/// Initialize new <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/> which creates this instane.</param>
		protected BaseLogDataSource(ILogDataSourceProvider provider)
		{
			provider.VerifyAccess();
			this.Id = nextId++;
			this.Logger = provider.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.Provider = provider;
			provider.SynchronizationContext.Post(this.ValidateSource);
		}


		/// <summary>
		/// Get <see cref="App"/> instance.
		/// </summary>
		public App Application { get => (App)this.Provider.Application; }


		/// <summary>
		/// Get or set name for displaying purpose.
		/// </summary>
		public string? DisplayName
		{
			get => this.displayName;
			protected set
			{
				this.VerifyAccess();
				if (this.IsDisposed || this.displayName == value)
					return;
				this.displayName = value;
				this.OnPropertyChanged(nameof(DisplayName));
			}
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			this.VerifyAccess();
			if (this.Provider is BaseLogDataSourceProvider baseProvider)
				baseProvider.NotifySourceDisposedInternal(this);
		}


		/// <summary>
		/// Get unique ID of this <see cref="BaseLogDataSource"/> instance.
		/// </summary>
		protected int Id { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		// Open stream reader asynchronously.
		public async Task<StreamReader> OpenReaderAsync()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsValid)
				throw new InvalidOperationException("Cannot open reader when IsValid is false.");

			// open reader
			this.Logger.LogDebug("Start opening reader");
			var reader = await Task.Run(this.OpenReaderCore);
			if (this.IsDisposed)
			{
				this.Logger.LogWarning("Instance has been disposed when opening reader");
				_ = Task.Run(() =>
				{
					try
					{
						reader.Dispose();
					}
					catch
					{ }
				});
				throw new ObjectDisposedException("Source has been disposed when opening reader.");
			}
			this.Logger.LogDebug("Reader opened");
			return reader;
		}


		/// <summary>
		/// Open <see cref="StreamReader"/> to read log data.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <returns><see cref="StreamReader"/>.</returns>
		protected abstract StreamReader OpenReaderCore();


		// Get readable string.
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";


		/// <summary>
		/// Start validating whether source is valid or not.
		/// </summary>
		protected async void ValidateSource()
		{
			this.VerifyAccess();
			if (this.IsDisposed)
				return;
			var isValid = await Task.Run(this.ValidateSourceCore);
			if (this.IsDisposed || this.IsValid == isValid)
				return;
			this.IsValid = isValid;
			this.OnPropertyChanged(nameof(IsValid));
		}


		/// <summary>
		/// Validate whether source is valid or not.
		/// </summary>
		/// <remarks>The method will be called in background thead.</remarks>
		/// <returns>True if source is valid.</returns>
		protected abstract bool ValidateSourceCore();


		// Interface implementations.
		public bool CheckAccess() => this.Provider.CheckAccess();
		IApplication IApplicationObject.Application { get => this.Application; }
		public abstract bool IsContinuousStream { get; }
		public bool IsValid { get; private set; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public ILogDataSourceProvider Provider { get; }
		public SynchronizationContext SynchronizationContext => this.Provider.SynchronizationContext;
		public UnderlyingLogDataSource UnderlyingSource => this.Provider.UnderlyingSource;
	}
}
