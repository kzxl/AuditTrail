using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuditTrail
{
    /// <summary>
    /// High-level audit context that combines change tracking with storage.
    /// Use this as the main entry point for auditing operations.
    /// </summary>
    /// <example>
    /// <code>
    /// var audit = new AuditContext(store, "admin");
    /// 
    /// // Track an update
    /// var snapshot = audit.Snapshot(product);
    /// product.Price = 999;
    /// audit.TrackUpdate(snapshot, product, "Products", product.Id.ToString());
    /// audit.SaveChanges();
    /// </code>
    /// </example>
    public class AuditContext : IDisposable
    {
        private readonly IAuditStore _store;
        private readonly string _userName;
        private readonly AuditOptions _options;
        private readonly List<AuditEntry> _pendingEntries = new List<AuditEntry>();

        /// <summary>
        /// Creates a new AuditContext.
        /// </summary>
        /// <param name="store">The audit store to persist entries to.</param>
        /// <param name="userName">The current user performing operations.</param>
        /// <param name="options">Optional audit configuration.</param>
        public AuditContext(IAuditStore store, string userName, AuditOptions options = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _userName = userName;
            _options = options ?? new AuditOptions();
        }

        /// <summary>
        /// Number of pending (unsaved) audit entries.
        /// </summary>
        public int PendingCount => _pendingEntries.Count;

        /// <summary>
        /// Creates a property snapshot of an entity for later comparison.
        /// </summary>
        public Dictionary<string, object> Snapshot<T>(T entity) where T : class
        {
            return ChangeTracker.Snapshot(entity);
        }

        /// <summary>
        /// Optional reason applied to all audit entries recorded within this context.
        /// </summary>
        public string CurrentAuditReason { get; set; }

        /// <summary>
        /// Optional transaction correlation identifier applied to all audit entries in this context.
        /// </summary>
        public string CurrentCorrelationId { get; set; }

        /// <summary>
        /// Tracks an insert operation with automated table and primary key resolution.
        /// </summary>
        public AuditContext TrackInsert<T>(T entity) where T : class
        {
            var entry = ChangeTracker.TrackInsert(entity, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks an insert operation.
        /// </summary>
        public AuditContext TrackInsert<T>(T entity, string tableName, string primaryKey) where T : class
        {
            var entry = ChangeTracker.TrackInsert(entity, tableName, primaryKey, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks a delete operation with automated table and primary key resolution.
        /// </summary>
        public AuditContext TrackDelete<T>(T entity) where T : class
        {
            var entry = ChangeTracker.TrackDelete(entity, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks a delete operation.
        /// </summary>
        public AuditContext TrackDelete<T>(T entity, string tableName, string primaryKey) where T : class
        {
            var entry = ChangeTracker.TrackDelete(entity, tableName, primaryKey, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks an update by comparing a snapshot with current entity state using automated table and key resolution.
        /// </summary>
        public AuditContext TrackUpdate<T>(Dictionary<string, object> snapshot, T current) where T : class
        {
            var entry = ChangeTracker.DetectChangesFromSnapshot(snapshot, current, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks an update by comparing a snapshot with the current entity state.
        /// </summary>
        public AuditContext TrackUpdate<T>(
            Dictionary<string, object> snapshot,
            T current,
            string tableName,
            string primaryKey) where T : class
        {
            var entry = ChangeTracker.DetectChangesFromSnapshot(
                snapshot, current, tableName, primaryKey, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks an update by comparing two entity instances using automated table and key resolution.
        /// </summary>
        public AuditContext TrackUpdate<T>(T original, T modified) where T : class
        {
            var entry = ChangeTracker.DetectChanges(original, modified, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Tracks an update by comparing two entity instances.
        /// </summary>
        public AuditContext TrackUpdate<T>(
            T original,
            T modified,
            string tableName,
            string primaryKey) where T : class
        {
            var entry = ChangeTracker.DetectChanges(
                original, modified, tableName, primaryKey, _userName, _options);
            return AddEntry(entry);
        }

        /// <summary>
        /// Adds a custom audit entry directly.
        /// </summary>
        public AuditContext AddEntry(AuditEntry entry)
        {
            if (entry != null)
            {
                if (string.IsNullOrEmpty(entry.AuditReason) && !string.IsNullOrEmpty(CurrentAuditReason))
                    entry.AuditReason = CurrentAuditReason;

                if (string.IsNullOrEmpty(entry.CorrelationId) && !string.IsNullOrEmpty(CurrentCorrelationId))
                    entry.CorrelationId = CurrentCorrelationId;

                _pendingEntries.Add(entry);
            }
            return this;
        }

        /// <summary>
        /// Saves all pending audit entries to the store.
        /// </summary>
        public void SaveChanges()
        {
            if (_pendingEntries.Count == 0) return;
            _store.SaveBatch(_pendingEntries);
            _pendingEntries.Clear();
        }

        /// <summary>
        /// Saves all pending audit entries asynchronously.
        /// </summary>
        public async Task SaveChangesAsync()
        {
            if (_pendingEntries.Count == 0) return;
            await _store.SaveBatchAsync(_pendingEntries);
            _pendingEntries.Clear();
        }

        /// <summary>
        /// Gets the audit history for a specific record.
        /// </summary>
        public IEnumerable<AuditEntry> GetHistory(string tableName, string primaryKey)
        {
            return _store.GetHistory(tableName, primaryKey);
        }

        /// <summary>
        /// Discards all pending entries.
        /// </summary>
        public void DiscardChanges()
        {
            _pendingEntries.Clear();
        }

        public void Dispose()
        {
            _pendingEntries.Clear();
        }
    }
}
