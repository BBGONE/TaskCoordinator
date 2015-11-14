using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Transactions;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Linq;

namespace Bell.PPS.Database.Shared
{
    /// <summary>
    /// Options for modifying how DbConnectionScope.Current is affected while constructing a new scope.
    /// </summary>
    public enum DbConnectionScopeOption
    {
        Required,                   // Set self as currentScope if there isn't one already on the thread, otherwise don't do anything.
        RequiresNew                 // Push self as currentScope (track prior scope and restore it on dispose).
    }

    // Allows almost-automated re-use of connections across multiple call levels
    //  while still controlling connection lifetimes.  Multiple connections are supported within a single scope.
    // To use:
    //  Create a new connection scope object in a using statement at the level within which you 
    //      want to scope connections.
    //  Use Current.AddConnection() and Current.GetConnection() to store/retrieve specific connections based on your
    //      own keys.
    //  Simpler alternative: Use Current.GetOpenConnection(factory, connection string) where you need to use the connection
    //
    // Example of simple case:
    //  void TopLevel() {
    //      using (DbConnectionScope scope = new DbConnectionScope()) {
    //          // Code that eventually calls LowerLevel a couple of times.
    //          // The first time LowerLevel is called, it will allocate and open the connection
    //          // Subsequent calls will use the already-opened connection, INCLUDING running in the same 
    //          //   System.Transactions transaction without using DTC (assuming only one connection string)!
    //      }
    //  }
    //
    //  void LowerLevel() {
    //      string connectionString = <...get connection string from config or somewhere...>;
    //      SqlCommand cmd = new SqlCommand("Some TSQL code");
    //      cmd.Connection = (SqlConnection) DbConnectionScope.Current.GetOpenConnection(SqlClientFactory.Instance, connectionString);
    //      ... finish setting up command and execute it
    //  }

    /// <summary>
    /// Class to assist in managing connection lifetimes inside scopes on a particular thread.
    /// </summary>
    public sealed class DbConnectionScope : IDisposable
    {
        private static readonly string SLOT_KEY = Guid.NewGuid().ToString();

#if TEST
        //For Testing Purposes
        public static int GetScopeStoreCount() {
            return _scopeStore.Count;
        }
#endif

        #region class fields
        private static ConcurrentDictionary<Guid, DbConnectionScope> _scopeStore = new ConcurrentDictionary<Guid, DbConnectionScope>();

        private static DbConnectionScope __currentScope
        {
            get
            {
                object res = CallContext.LogicalGetData(SLOT_KEY);
                if (res != null)
                {
                    Guid scopeID = (Guid)res;
                    DbConnectionScope scope;
                    if (_scopeStore.TryGetValue(scopeID, out scope))
                        return scope;
                    else
                    {
                        return null;
                    }
                }
                return null;
            }
            set
            {

                Guid? id = value == null ? (Guid?)null : value.UNIQUE_ID;
                if (id.HasValue)
                    CallContext.LogicalSetData(SLOT_KEY, id);
                else
                    CallContext.LogicalSetData(SLOT_KEY, null);
            }
        }
#endregion

#region instance fields
        internal readonly Guid UNIQUE_ID = Guid.NewGuid();
        private object SyncRoot = new object();
        private DbConnectionScope _outerScope;    // outer scope in stack of scopes on this call context
        private ConcurrentDictionary<string, DbConnection> _connections;   // set of connections contained by this scope.
        private bool _isDisposed; 
#endregion

#region public class methods and properties
        /// <summary>
        /// Obtain the currently active connection scope
        /// </summary>
        public static DbConnectionScope Current
        {
            get
            {
                return __currentScope;
            }
        }

#endregion

#region public instance methods and properties

        /// <summary>
        /// Default Constructor
        /// </summary>
        public DbConnectionScope() : this(DbConnectionScopeOption.Required)
        {
        }

        /// <summary>
        /// Constructor with options
        /// </summary>
        /// <param name="option">Option for how to modify Current during constructor</param>
        public DbConnectionScope(DbConnectionScopeOption option)
        {
            _isDisposed = true;  // short circuit Dispose until we're properly set up
            if (option == DbConnectionScopeOption.RequiresNew || (option == DbConnectionScopeOption.Required && __currentScope == null))
            {
                // only bother allocating dictionary if we're going to push
                _connections = new ConcurrentDictionary<string, DbConnection>();

                // Devnote:  Order of initial assignment is important in cases of failure!
                //  _priorScope first makes sure we know who we need to restore
                //  _isDisposed second, to make sure we no-op dispose until we're as close to
                //   correct setup as possible (i.e. all other instance fields set prior to _isDisposed = false)
                //  __currentScope last, to make sure the thread static only holds validly set up objects
                _outerScope = __currentScope;
                __currentScope = this;
                if (_scopeStore.TryAdd(this.UNIQUE_ID, this))
                    _isDisposed = false;
            }
        }

        /// <summary>
        /// Shut down this instance.  Disposes all connections it holds and restores the prior scope.
        /// </summary>
        public void Dispose()
        {
            lock (this.SyncRoot)
            {
                if (!_isDisposed)
                {
                    // Firstly, remove ourselves from the stack (but, only if we are the one on the stack)
                    //  Note: Thread-local _currentScope, and requirement that scopes not be disposed on other threads
                    //      means we can get away with not locking.  Worst case is that we corrupt the connections
                    //      and the IDictionary contained in the scope that's being disposed on two threads -- we don't
                    //      corrupt the __currentScope stack because we don't touch it unless this instance is in said stack!
                    // In case the user called dispose out of order, skip up the chain until we find
                    //  an undisposed scope.
                    if (_isDisposed)
                        return;
                    DbConnectionScope outerScope = _outerScope;
                    while (outerScope != null && outerScope._isDisposed)
                    {
                        outerScope = outerScope._outerScope;
                    }
                    try
                    {
                        DbConnectionScope tmp;
                        _scopeStore.TryRemove(this.UNIQUE_ID, out tmp);
                        __currentScope = outerScope;
                    }
                    finally
                    {
                        // secondly, make sure our internal state is set to "Disposed"
                        _isDisposed = true;

                        var connections = _connections.Values.ToArray();
                        _connections.Clear();
                        _connections = null;

                        // Lastly, clean up the connections we own
                        foreach (DbConnection connection in connections)
                        {
                            if (connection.State != ConnectionState.Closed)
                            {
                                connection.Dispose();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the connection associated with this key.
        /// </summary>
        /// <param name="connectionString">Key to use for lookup</param>
        /// <param name="connection">Associated connection</param>
        /// <returns>True if connection found, false otherwise</returns>
        public bool TryGetConnection(string connectionString, out DbConnection connection)
        {
            bool found = false;
            lock (this.SyncRoot)
            {
                CheckDisposed();
                found = _connections.TryGetValue(connectionString, out connection);
                var currTran = Transaction.Current;
                if (found && (currTran == null || currTran.TransactionInformation.Status != TransactionStatus.Active))
                {
                    DbConnection tmp;
                    _connections.TryRemove(connectionString, out tmp);
                    connection.Dispose();
                    connection = null;
                    found = false;
                }
            }
            return found;
        }

        public DbConnection GetConnection(DbProviderFactory factory, string connectionString)
        {
            DbConnection result = null;
            lock (this.SyncRoot)
            {
                if (!TryGetConnection(connectionString, out result))
                {
                    result = factory.CreateConnection();
                    result.ConnectionString = connectionString;
                    _connections.TryAdd(connectionString, result);
                }
            }

            return result;
        }

        /// <summary>
        /// This method gets the connection using the connection string as a key.  If no connection is
        /// associated with the string, the connection factory is used to create the connection.
        /// Finally, if the resulting connection is in the closed state, it is opened.
        /// </summary>
        /// <param name="factory">Factory to use to create connection if it is not already present</param>
        /// <param name="connectionString">Connection string to use</param>
        /// <returns>Connection in open state</returns>
        public DbConnection GetOpenConnection(DbProviderFactory factory, string connectionString)
        {
            DbConnection result = this.GetConnection(factory, connectionString);
            // however we got it, open it if it's closed.
            //  note: don't open unless state is unambiguous that it's ok to open
            if (result.State == ConnectionState.Closed)
                result.Open();
            return result;
        }

        /// <summary>
        /// This method gets the connection using the connection string as a key.  If no connection is
        /// associated with the string, the connection factory is used to create the connection.
        /// Finally, if the resulting connection is in the closed state, it is opened.
        /// </summary>
        /// <param name="factory">Factory to use to create connection if it is not already present</param>
        /// <param name="connectionString">Connection string to use</param>
        /// <returns>Connection in open state</returns>
        public async Task<DbConnection> GetOpenConnectionAsync(DbProviderFactory factory, string connectionString)
        {
            DbConnection result = this.GetConnection(factory, connectionString);
            if (result.State == ConnectionState.Closed)
                await result.OpenAsync();
            return result;
        }

        public bool IsDisposed
        {
            get
            {
                return _isDisposed;
            }
        }

#endregion

#region private methods and properties
        /// <summary>
        /// Handle calling API function after instance has been disposed
        /// </summary>
        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("DbConnectionScope");
            }
        }
#endregion
    }
}