using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuditTrail
{
    /// <summary>
    /// Interface for persisting audit entries.
    /// Implement this interface for custom storage backends (SQL Server, MongoDB, file, etc.)
    /// </summary>
    public interface IAuditStore
    {
        /// <summary>
        /// Saves a single audit entry.
        /// </summary>
        void Save(AuditEntry entry);

        /// <summary>
        /// Saves a single audit entry asynchronously.
        /// </summary>
        Task SaveAsync(AuditEntry entry);

        /// <summary>
        /// Saves multiple audit entries in a batch.
        /// </summary>
        void SaveBatch(IEnumerable<AuditEntry> entries);

        /// <summary>
        /// Saves multiple audit entries asynchronously.
        /// </summary>
        Task SaveBatchAsync(IEnumerable<AuditEntry> entries);

        /// <summary>
        /// Ensures the audit storage tables/indexes exist.
        /// Call this once during application startup.
        /// </summary>
        void EnsureCreated();

        /// <summary>
        /// Queries audit entries for a specific record.
        /// </summary>
        /// <param name="tableName">Table name to search.</param>
        /// <param name="primaryKey">Primary key value.</param>
        /// <returns>List of audit entries, ordered by timestamp descending.</returns>
        IEnumerable<AuditEntry> GetHistory(string tableName, string primaryKey);

        /// <summary>
        /// Queries audit entries for a specific table.
        /// </summary>
        /// <param name="tableName">Table name to search.</param>
        /// <param name="top">Maximum number of entries to return.</param>
        /// <returns>List of recent audit entries.</returns>
        IEnumerable<AuditEntry> GetTableHistory(string tableName, int top = 100);
    }
}
