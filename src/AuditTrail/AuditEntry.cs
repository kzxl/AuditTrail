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

        /// <summary>Optional reason or business justification for this change.</summary>
        public string AuditReason { get; set; }

        /// <summary>Optional correlation identifier linking multiple changes in a single business transaction.</summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Serialized JSON representation of <see cref="Changes"/>, used for Single-Table storage mode.
        /// </summary>
        public string ChangesJson
        {
            get => _changesJson ?? BuildChangesJson();
            set => _changesJson = value;
        }
        private string _changesJson;

        /// <summary>
        /// Generates a compact JSON string representing all field changes without external JSON dependencies.
        /// </summary>
        public string BuildChangesJson()
        {
            if (Changes == null || Changes.Count == 0)
                return "[]";

            var sb = new System.Text.StringBuilder(Changes.Count * 64);
            sb.Append("[");
            for (int i = 0; i < Changes.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var c = Changes[i];
                sb.Append("{\"Field\":");
                EscapeJsonString(sb, c.FieldName);
                sb.Append(",\"Old\":");
                EscapeJsonString(sb, c.OldValue);
                sb.Append(",\"New\":");
                EscapeJsonString(sb, c.NewValue);
                sb.Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static void EscapeJsonString(System.Text.StringBuilder sb, string str)
        {
            if (str == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append("\"");
            foreach (char ch in str)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < 32)
                            sb.AppendFormat("\\u{0:X4}", (int)ch);
                        else
                            sb.Append(ch);
                        break;
                }
            }
            sb.Append("\"");
        }
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
