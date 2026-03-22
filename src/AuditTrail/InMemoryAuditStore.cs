using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuditTrail
{
    /// <summary>
    /// In-memory implementation of <see cref="IAuditStore"/> for testing.
    /// </summary>
    public class InMemoryAuditStore : IAuditStore
    {
        private readonly List<AuditEntry> _entries = new List<AuditEntry>();
        private long _nextId = 1;

        /// <summary>All stored entries (for assertions).</summary>
        public IReadOnlyList<AuditEntry> Entries => _entries.AsReadOnly();

        public void EnsureCreated() { }

        public void Save(AuditEntry entry)
        {
            if (entry == null) return;
            entry.Id = _nextId++;
            _entries.Add(entry);
        }

        public Task SaveAsync(AuditEntry entry)
        {
            Save(entry);
            return Task.CompletedTask;
        }

        public void SaveBatch(IEnumerable<AuditEntry> entries)
        {
            foreach (var e in entries) Save(e);
        }

        public Task SaveBatchAsync(IEnumerable<AuditEntry> entries)
        {
            SaveBatch(entries);
            return Task.CompletedTask;
        }

        public IEnumerable<AuditEntry> GetHistory(string tableName, string primaryKey)
        {
            return _entries
                .Where(e => e.TableName == tableName && e.PrimaryKey == primaryKey)
                .OrderByDescending(e => e.Timestamp);
        }

        public IEnumerable<AuditEntry> GetTableHistory(string tableName, int top = 100)
        {
            return _entries
                .Where(e => e.TableName == tableName)
                .OrderByDescending(e => e.Timestamp)
                .Take(top);
        }

        /// <summary>Clears all entries.</summary>
        public void Clear() => _entries.Clear();
    }
}
