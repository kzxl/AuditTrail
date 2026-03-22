using System;
using System.Collections.Generic;

namespace AuditTrail
{
    /// <summary>
    /// Represents a single audit log entry recording a data change.
    /// </summary>
    public class AuditEntry
    {
        /// <summary>Unique identifier for this audit entry.</summary>
        public long Id { get; set; }

        /// <summary>Name of the table that was modified.</summary>
        public string TableName { get; set; }

        /// <summary>Primary key value of the affected record.</summary>
        public string PrimaryKey { get; set; }

        /// <summary>Type of operation: Insert, Update, or Delete.</summary>
        public AuditAction Action { get; set; }

        /// <summary>User who performed the change.</summary>
        public string UserName { get; set; }

        /// <summary>Timestamp of the change (UTC).</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Individual field changes within this entry.</summary>
        public List<AuditFieldChange> Changes { get; set; } = new List<AuditFieldChange>();

        /// <summary>Optional metadata (IP address, machine name, etc.)</summary>
        public string Metadata { get; set; }
    }

    /// <summary>
    /// Represents a single field change within an audit entry.
    /// </summary>
    public class AuditFieldChange
    {
        /// <summary>Name of the changed field/column.</summary>
        public string FieldName { get; set; }

        /// <summary>Value before the change (null for inserts).</summary>
        public string OldValue { get; set; }

        /// <summary>Value after the change (null for deletes).</summary>
        public string NewValue { get; set; }
    }

    /// <summary>
    /// Type of audit action.
    /// </summary>
    public enum AuditAction
    {
        /// <summary>A new record was inserted.</summary>
        Insert = 1,

        /// <summary>An existing record was updated.</summary>
        Update = 2,

        /// <summary>A record was deleted.</summary>
        Delete = 3
    }
}
