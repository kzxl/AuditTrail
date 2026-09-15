using System;
using System.Collections.Generic;

namespace AuditTrail
{
    /// <summary>
    /// Configuration options for the audit trail system.
    /// </summary>
    public class AuditOptions
    {
        /// <summary>
        /// Tables to include in auditing. If empty, all tables are audited.
        /// </summary>
        public HashSet<string> IncludeTables { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tables to exclude from auditing.
        /// </summary>
        public HashSet<string> ExcludeTables { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fields to exclude from auditing (format: "TableName.FieldName" or just "FieldName" for all tables).
        /// Common exclusions: password hashes, large binary fields.
        /// </summary>
        public HashSet<string> ExcludeFields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whether to record the old values for deleted records.
        /// </summary>
        public bool TrackDeletedValues { get; set; } = true;

        /// <summary>
        /// Whether to record all field values for inserted records.
        /// </summary>
        public bool TrackInsertedValues { get; set; } = true;

        /// <summary>
        /// Maximum length for string values stored in OldValue/NewValue.
        /// Values exceeding this length are truncated. Default: 4000.
        /// </summary>
        public int MaxValueLength { get; set; } = 4000;

        /// <summary>
        /// Schema name for the audit log table. Default: "dbo".
        /// </summary>
        public string SchemaName { get; set; } = "dbo";

        /// <summary>
        /// Table name for the audit log entries. Default: "AuditLog".
        /// </summary>
        public string AuditTableName { get; set; } = "AuditLog";

        /// <summary>
        /// Table name for the audit field changes. Default: "AuditLogDetail".
        /// </summary>
        public string AuditDetailTableName { get; set; } = "AuditLogDetail";

        /// <summary>
        /// Persistence model for audit records: Normalized tables (default) or SingleTableJson payload.
        /// </summary>
        public AuditStorageMode StorageMode { get; set; } = AuditStorageMode.NormalizedTables;

        /// <summary>
        /// Whether to use compiled lambda expression delegates instead of standard reflection for reading properties. Default: true.
        /// </summary>
        public bool UseCompiledAccessors { get; set; } = true;

        /// <summary>
        /// Whether to compare values using typed fast equality before converting to strings. Default: true.
        /// </summary>
        public bool EnableFastEquality { get; set; } = true;

        /// <summary>
        /// Checks if a table should be audited.
        /// </summary>
        public bool ShouldAuditTable(string tableName)
        {
            if (ExcludeTables.Contains(tableName))
                return false;
            if (IncludeTables.Count > 0)
                return IncludeTables.Contains(tableName);
            return true;
        }

        /// <summary>
        /// Checks if a field should be audited.
        /// </summary>
        public bool ShouldAuditField(string tableName, string fieldName)
        {
            return !ExcludeFields.Contains(fieldName)
                && !ExcludeFields.Contains($"{tableName}.{fieldName}");
        }
    }

    /// <summary>
    /// Storage format strategy for audit change records.
    /// </summary>
    public enum AuditStorageMode
    {
        /// <summary>
        /// Stores audit metadata in AuditLog and field differences in AuditLogDetail (normalized).
        /// </summary>
        NormalizedTables = 1,

        /// <summary>
        /// Stores audit metadata and a compact JSON payload of changes in AuditLog only.
        /// Reduces database I/O by up to 80% and avoids multi-row detail table insertion.
        /// </summary>
        SingleTableJson = 2,

        /// <summary>
        /// Writes to both normalized detail tables and the combined JSON column.
        /// </summary>
        Both = 3
    }
}
