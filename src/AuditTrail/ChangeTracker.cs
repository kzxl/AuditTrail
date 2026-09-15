using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AuditTrail
{
    /// <summary>
    /// High-performance entity change detection engine.
    /// Utilizes compiled expression delegates and typed fast-equality checks to eliminate reflection and GC overhead.
    /// </summary>
    public static class ChangeTracker
    {
        /// <summary>
        /// Detects changes between two instances of the same type with automated table and key resolution.
        /// </summary>
        public static AuditEntry DetectChanges<T>(
            T original,
            T modified,
            string userName,
            AuditOptions options = null) where T : class
        {
            var meta = AuditTypeMetadata.Get<T>();
            var entity = modified ?? original;
            var tableName = meta.DefaultTableName;
            var primaryKey = meta.ResolvePrimaryKey(entity) ?? "0";
            return DetectChanges(original, modified, tableName, primaryKey, userName, options);
        }

        /// <summary>
        /// Detects changes between two instances of the same type.
        /// </summary>
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
            var meta = AuditTypeMetadata.Get<T>();

            foreach (var prop in meta.AuditableProperties)
            {
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;

                var oldVal = original != null ? prop.Getter(original) : null;
                var newVal = modified != null ? prop.Getter(modified) : null;

                if (options.EnableFastEquality && FastEquals(oldVal, newVal))
                    continue;

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
        /// Creates an audit entry for a new record insertion with automated table and key resolution.
        /// </summary>
        public static AuditEntry TrackInsert<T>(
            T entity,
            string userName,
            AuditOptions options = null) where T : class
        {
            var meta = AuditTypeMetadata.Get<T>();
            var tableName = meta.DefaultTableName;
            var primaryKey = meta.ResolvePrimaryKey(entity) ?? "0";
            return TrackInsert(entity, tableName, primaryKey, userName, options);
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
            var meta = AuditTypeMetadata.Get<T>();

            foreach (var prop in meta.AuditableProperties)
            {
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;

                var val = prop.Getter(entity);
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
        /// Creates an audit entry for a record deletion with automated table and key resolution.
        /// </summary>
        public static AuditEntry TrackDelete<T>(
            T entity,
            string userName,
            AuditOptions options = null) where T : class
        {
            var meta = AuditTypeMetadata.Get<T>();
            var tableName = meta.DefaultTableName;
            var primaryKey = meta.ResolvePrimaryKey(entity) ?? "0";
            return TrackDelete(entity, tableName, primaryKey, userName, options);
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
                var meta = AuditTypeMetadata.Get<T>();

                foreach (var prop in meta.AuditableProperties)
                {
                    if (!options.ShouldAuditField(tableName, prop.Name)) continue;

                    var val = prop.Getter(entity);
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
        /// Creates a property snapshot using compiled accessors for later comparison.
        /// </summary>
        public static Dictionary<string, object> Snapshot<T>(T entity) where T : class
        {
            if (entity == null) return null;

            var meta = AuditTypeMetadata.Get<T>();
            var snapshot = new Dictionary<string, object>(meta.AuditableProperties.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var prop in meta.AuditableProperties)
            {
                snapshot[prop.Name] = prop.Getter(entity);
            }

            return snapshot;
        }

        /// <summary>
        /// Detects changes between a snapshot and the current entity state with automated table and key resolution.
        /// </summary>
        public static AuditEntry DetectChangesFromSnapshot<T>(
            Dictionary<string, object> snapshot,
            T current,
            string userName,
            AuditOptions options = null) where T : class
        {
            var meta = AuditTypeMetadata.Get<T>();
            var tableName = meta.DefaultTableName;
            var primaryKey = meta.ResolvePrimaryKey(current) ?? "0";
            return DetectChangesFromSnapshot(snapshot, current, tableName, primaryKey, userName, options);
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
            var meta = AuditTypeMetadata.Get<T>();

            foreach (var prop in meta.AuditableProperties)
            {
                if (!options.ShouldAuditField(tableName, prop.Name)) continue;

                snapshot.TryGetValue(prop.Name, out var oldVal);
                var newVal = prop.Getter(current);

                if (options.EnableFastEquality && FastEquals(oldVal, newVal))
                    continue;

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

        /// <summary>
        /// Compares two values directly to avoid unnecessary string conversions.
        /// </summary>
        private static bool FastEquals(object oldVal, object newVal)
        {
            if (ReferenceEquals(oldVal, newVal)) return true;
            if (oldVal == null || newVal == null) return false;

            if (oldVal is byte[] b1 && newVal is byte[] b2)
            {
                if (b1.Length != b2.Length) return false;
                for (int i = 0; i < b1.Length; i++)
                {
                    if (b1[i] != b2[i]) return false;
                }
                return true;
            }

            return oldVal.Equals(newVal);
        }

        public static bool IsSimpleType(Type type)
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

        public static string FormatValue(object value, int maxLength)
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
