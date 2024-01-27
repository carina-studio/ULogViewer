using CarinaStudio.Threading.Tasks;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Data;

/// <summary>
/// Utility for SQLite.
/// </summary>
public static class SQLite
{
    // Fields.
    static TaskFactory? _DatabaseTaskFactory;


    /// <summary>
    /// Get <see cref="TaskFactory"/> for tasks to access SQLite database.
    /// </summary>
    public static TaskFactory DatabaseTaskFactory
    {
        get
        {
            if (_DatabaseTaskFactory is not null)
                return _DatabaseTaskFactory;
            lock (typeof(SQLite))
            {
                _DatabaseTaskFactory ??= new FixedThreadsTaskFactory(1);
            }
            return _DatabaseTaskFactory;
        }
    }
}