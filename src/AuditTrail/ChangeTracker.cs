using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AuditTrail
{
    /// <summary>
    /// Tracks changes between original and modified entity snapshots.
    /// Works with any POCO class — no base class or interface required.
    /// </summary>
    public static class ChangeTracker
    {
        /// <summary>
        /// Detects changes between two instances of the same type.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
        /// <param name="original">Original state (before changes).</param>
        /// <param name="modified">Modified state (after changes).</param>
        /// <param name="tableName">Table name for the audit entry.</param>
        /// <param name="primaryKey">Primary key value identifier.</param>
        /// <param name="userName">User who made the change.</param>
        /// <param name="options">Optional audit options for filtering.</param>
        /// <returns>AuditEntry with detected changes, or null if no changes found.</returns>
        public static AuditEntry DetectChanges<T>(
            T original,
            T modified,
            string tableName,
            string primaryKey,
            string userName,
            AuditOptions options = null) where T : class
        {
            options = options ?? new AuditOptions();

            if (!options.ShouldAuditTable(tableName))
                return null;

            var changes = new List<AuditFieldChange>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead) continue;
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;

                // Skip complex types (navigation properties)
                if (!IsSimpleType(prop.PropertyType)) continue;

                var oldVal = original != null ? prop.GetValue(original) : null;
                var newVal = modified != null ? prop.GetValue(modified) : null;

                var oldStr = FormatValue(oldVal, options.MaxValueLength);
                var newStr = FormatValue(newVal, options.MaxValueLength);

                if (!string.Equals(oldStr, newStr, StringComparison.Ordinal))
                {
                    changes.Add(new AuditFieldChange
                    {
                        FieldName = prop.Name,
                        OldValue = oldStr,
                        NewValue = newStr
                    });
                }
            }

            if (changes.Count == 0)
                return null;

            return new AuditEntry
            {
                TableName = tableName,
                PrimaryKey = primaryKey,
                Action = original == null ? AuditAction.Insert
                       : modified == null ? AuditAction.Delete
                       : AuditAction.Update,
                UserName = userName,
                Changes = changes
            };
        }

        /// <summary>
        /// Creates an audit entry for a new record insertion.
        /// </summary>
        public static AuditEntry TrackInsert<T>(
            T entity,
            string tableName,
            string primaryKey,
            string userName,
            AuditOptions options = null) where T : class
        {
            options = options ?? new AuditOptions();

            if (!options.ShouldAuditTable(tableName) || !options.TrackInsertedValues)
                return null;

            var changes = new List<AuditFieldChange>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead) continue;
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;
                if (!IsSimpleType(prop.PropertyType)) continue;

                var val = prop.GetValue(entity);
                if (val == null) continue;

                changes.Add(new AuditFieldChange
                {
                    FieldName = prop.Name,
                    OldValue = null,
                    NewValue = FormatValue(val, options.MaxValueLength)
                });
            }

            return new AuditEntry
            {
                TableName = tableName,
                PrimaryKey = primaryKey,
                Action = AuditAction.Insert,
                UserName = userName,
                Changes = changes
            };
        }

        /// <summary>
        /// Creates an audit entry for a record deletion.
        /// </summary>
        public static AuditEntry TrackDelete<T>(
            T entity,
            string tableName,
            string primaryKey,
            string userName,
            AuditOptions options = null) where T : class
        {
            options = options ?? new AuditOptions();

            if (!options.ShouldAuditTable(tableName))
                return null;

            var changes = new List<AuditFieldChange>();

            if (options.TrackDeletedValues)
            {
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in properties)
                {
                    if (!prop.CanRead) continue;
                    if (!options.ShouldAuditField(tableName, prop.Name)) continue;
                    if (!IsSimpleType(prop.PropertyType)) continue;

                    var val = prop.GetValue(entity);
                    if (val == null) continue;

                    changes.Add(new AuditFieldChange
                    {
                        FieldName = prop.Name,
                        OldValue = FormatValue(val, options.MaxValueLength),
                        NewValue = null
                    });
                }
            }

            return new AuditEntry
            {
                TableName = tableName,
                PrimaryKey = primaryKey,
                Action = AuditAction.Delete,
                UserName = userName,
                Changes = changes
            };
        }

        /// <summary>
        /// Creates a snapshot (deep copy of property values) for later comparison.
        /// </summary>
        public static Dictionary<string, object> Snapshot<T>(T entity) where T : class
        {
            if (entity == null) return null;

            var snapshot = new Dictionary<string, object>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead) continue;
                if (!IsSimpleType(prop.PropertyType)) continue;
                snapshot[prop.Name] = prop.GetValue(entity);
            }

            return snapshot;
        }

        /// <summary>
        /// Detects changes between a snapshot and the current entity state.
        /// </summary>
        public static AuditEntry DetectChangesFromSnapshot<T>(
            Dictionary<string, object> snapshot,
            T current,
            string tableName,
            string primaryKey,
            string userName,
            AuditOptions options = null) where T : class
        {
            options = options ?? new AuditOptions();

            if (!options.ShouldAuditTable(tableName))
                return null;

            var changes = new List<AuditFieldChange>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead) continue;
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;
                if (!IsSimpleType(prop.PropertyType)) continue;

                snapshot.TryGetValue(prop.Name, out var oldVal);
                var newVal = prop.GetValue(current);

                var oldStr = FormatValue(oldVal, options.MaxValueLength);
                var newStr = FormatValue(newVal, options.MaxValueLength);

                if (!string.Equals(oldStr, newStr, StringComparison.Ordinal))
                {
                    changes.Add(new AuditFieldChange
                    {
                        FieldName = prop.Name,
                        OldValue = oldStr,
                        NewValue = newStr
                    });
                }
            }

            if (changes.Count == 0)
                return null;

            return new AuditEntry
            {
                TableName = tableName,
                PrimaryKey = primaryKey,
                Action = AuditAction.Update,
                UserName = userName,
                Changes = changes
            };
        }

        // ─── Helpers ──────────────────────────────────────────────

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid)
                || type == typeof(byte[]);
        }

        private static string FormatValue(object value, int maxLength)
        {
            if (value == null) return null;

            string str;
            if (value is DateTime dt)
                str = dt.ToString("O"); // ISO 8601
            else if (value is DateTimeOffset dto)
                str = dto.ToString("O");
            else if (value is byte[] bytes)
                str = $"[{bytes.Length} bytes]";
            else
                str = value.ToString();

            if (str.Length > maxLength)
                str = str.Substring(0, maxLength);

            return str;
        }
    }
}
