using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Logger it keep logs in memory.
	/// </summary>
	static class MemoryLogger
	{
		// Log holder.
		class LogHolder
		{
			public readonly long Index;
			public readonly CompressedString? Log;
			public volatile LogHolder? Next;
			public LogHolder(long index, string log)
			{
				this.Index = index;
				this.Log = CompressedString.Create(log, CompressedString.Level.Optimal);
			}
		}


		// Enumerator of log holder.
		class LogHolderEnumerator : IEnumerable<string>, IEnumerator<string>
		{
			// Fields.
			string? current;
			public volatile LogHolder? CurrentLogHolder;

			// Implementations.
			public string Current { get => this.current ?? throw new InvalidOperationException(); }
			public void Dispose()
			{
				this.CurrentLogHolder = null;
				OnLogHolderEnumeratorDisposed(this);
			}
			public IEnumerator<string> GetEnumerator() => this;
			object IEnumerator.Current => this.Current;
			IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
			public bool MoveNext()
			{
				lock (logHolderLock)
				{
					this.current = null;
					while (true)
					{
						if (this.CurrentLogHolder == null)
						{
							if (logHolderListHead != null)
							{
								this.CurrentLogHolder = logHolderListHead;
								break;
							}
							else
								Monitor.Wait(logHolderLock);
						}
						else if (this.CurrentLogHolder.Next != null)
						{
							this.CurrentLogHolder = this.CurrentLogHolder.Next;
							break;
						}
						else
							Monitor.Wait(logHolderLock);
					}
				}
				this.current = this.CurrentLogHolder.Log?.ToString() ?? "";
				return true;
			}
			public void Reset()
			{
				lock (logHolderLock)
				{
					this.current = null;
					this.CurrentLogHolder = null;
				}
			}
		}


		// Constants.
		const int LogHolderCapacity = 10000;
		const int LogHolderDropCount = 1000;


		// Fields.
		static volatile int logHolderCount;
		static readonly List<LogHolderEnumerator> logHolderEnumerators = new List<LogHolderEnumerator>();
		static volatile LogHolder? logHolderListHead;
		static volatile LogHolder? logHolderListTail;
		static readonly object logHolderLock = new object();
		static long nextLogHolderIndex = 1;


		// Drop logs which exceeds capacity.
		static void DropLogHolders()
		{
			if (logHolderCount <= LogHolderCapacity)
				return;
			lock (logHolderLock)
			{
				if (logHolderCount <= LogHolderCapacity)
					return;
				var dropCount = 0;
				var logHolder = logHolderListHead;
				var currentLogHolderIndex = logHolderEnumerators.Let(it =>
				{
					var index = -1L;
					foreach (var enumerator in it)
					{
						if (enumerator.CurrentLogHolder != null)
						{
							if (index < 0)
								index = enumerator.CurrentLogHolder.Index;
							else
								index = Math.Min(index, enumerator.CurrentLogHolder.Index);
						}
					}
					return index;
				});
				while (logHolder != null && dropCount < LogHolderDropCount)
				{
					if (currentLogHolderIndex >= 0 && logHolder.Index >= currentLogHolderIndex)
						break;
					var nextLogHolder = logHolder.Next;
					logHolderListHead = nextLogHolder;
					if (logHolderListTail == logHolder)
						logHolderListTail = null;
					logHolder.Next = null;
					logHolder = nextLogHolder;
					--logHolderCount;
					++dropCount;
				}
			}
		}


		/// <summary>
		/// Get <see cref="IEnumerable{String}"/> to enumerate logs.
		/// </summary>
		/// <returns><see cref="IEnumerable{String}"/>.</returns>
		public static IEnumerable<string> EnumerateLogs()
		{
			var enumerator = new LogHolderEnumerator();
			lock (logHolderLock)
				logHolderEnumerators.Add(enumerator);
			return enumerator;
		}


		/// <summary>
		/// Add log to memory.
		/// </summary>
		public static void Log(string timestamp, string pid, string tid, string level, string logger, string message, string exception)
		{
			var messages = Global.Run(() =>
			{
				if (string.IsNullOrEmpty(exception))
					return new string[] { message };
				message = $"{message} {exception}";
				return message.Split('\n');
			});
			lock (logHolderLock)
			{
				for (int i = 0, lineCount = messages.Length; i < lineCount; ++i)
				{
					var log = $"{timestamp} {pid} {tid} {level} {logger}: {messages[i]}";
					var logHolder = new LogHolder(nextLogHolderIndex++, log);
					if (logHolderListHead == null)
						logHolderListHead = logHolder;
					if (logHolderListTail != null)
						logHolderListTail.Next = logHolder;
					logHolderListTail = logHolder;
					++logHolderCount;
				}
				Monitor.PulseAll(logHolderLock);
				DropLogHolders();
			}
		}


		// Called when log holder enumerator disposed.
		static void OnLogHolderEnumeratorDisposed(LogHolderEnumerator enumerator)
		{
			lock (logHolderLock)
			{
				if (!logHolderEnumerators.Remove(enumerator))
					return;
				DropLogHolders();
			}
		}
	}
}
