using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace AuditTrail
{
    /// <summary>
    /// SQL Server implementation of <see cref="IAuditStore"/>.
    /// Stores audit entries in two tables: AuditLog (header) and AuditLogDetail (field changes).
    /// </summary>
    public class SqlServerAuditStore : IAuditStore
    {
        private readonly string _connectionString;
        private readonly AuditOptions _options;

        /// <summary>
        /// Creates a new SqlServerAuditStore.
        /// </summary>
        /// <param name="connectionString">SQL Server connection string.</param>
        /// <param name="options">Audit configuration options.</param>
        public SqlServerAuditStore(string connectionString, AuditOptions options = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _options = options ?? new AuditOptions();
        }

        /// <inheritdoc/>
        public void EnsureCreated()
        {
            using (var conn = CreateConnection())
            {
                conn.Execute(GetCreateTablesSql());
            }
        }

        /// <inheritdoc/>
        public void Save(AuditEntry entry)
        {
            if (entry == null) return;

            using (var conn = CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    SaveEntry(conn, tx, entry);
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task SaveAsync(AuditEntry entry)
        {
            if (entry == null) return;

            using (var conn = CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    await SaveEntryAsync(conn, tx, entry);
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public void SaveBatch(IEnumerable<AuditEntry> entries)
        {
            if (entries == null) return;
            var list = entries.Where(e => e != null).ToList();
            if (list.Count == 0) return;

            using (var conn = CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    foreach (var entry in list)
                    {
                        SaveEntry(conn, tx, entry);
                    }
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task SaveBatchAsync(IEnumerable<AuditEntry> entries)
        {
            if (entries == null) return;
            var list = entries.Where(e => e != null).ToList();
            if (list.Count == 0) return;

            using (var conn = CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    foreach (var entry in list)
                    {
                        await SaveEntryAsync(conn, tx, entry);
                    }
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<AuditEntry> GetHistory(string tableName, string primaryKey)
        {
            var sql = $@"
                SELECT a.Id, a.TableName, a.PrimaryKey, a.Action, a.UserName, a.Timestamp, a.Metadata
                FROM [{_options.SchemaName}].[{_options.AuditTableName}] a
                WHERE a.TableName = @TableName AND a.PrimaryKey = @PrimaryKey
                ORDER BY a.Timestamp DESC";

            var detailSql = $@"
                SELECT FieldName, OldValue, NewValue
                FROM [{_options.SchemaName}].[{_options.AuditDetailTableName}]
                WHERE AuditLogId = @Id";

            using (var conn = CreateConnection())
            {
                var entries = conn.Query<AuditEntry>(sql, new { TableName = tableName, PrimaryKey = primaryKey }).ToList();

                foreach (var entry in entries)
                {
                    entry.Changes = conn.Query<AuditFieldChange>(detailSql, new { entry.Id }).ToList();
                }

                return entries;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<AuditEntry> GetTableHistory(string tableName, int top = 100)
        {
            var sql = $@"
                SELECT TOP (@Top) a.Id, a.TableName, a.PrimaryKey, a.Action, a.UserName, a.Timestamp, a.Metadata
                FROM [{_options.SchemaName}].[{_options.AuditTableName}] a
                WHERE a.TableName = @TableName
                ORDER BY a.Timestamp DESC";

            var detailSql = $@"
                SELECT FieldName, OldValue, NewValue
                FROM [{_options.SchemaName}].[{_options.AuditDetailTableName}]
                WHERE AuditLogId = @Id";

            using (var conn = CreateConnection())
            {
                var entries = conn.Query<AuditEntry>(sql, new { TableName = tableName, Top = top }).ToList();

                foreach (var entry in entries)
                {
                    entry.Changes = conn.Query<AuditFieldChange>(detailSql, new { entry.Id }).ToList();
                }

                return entries;
            }
        }

        // ─── Internal ─────────────────────────────────────────────

        private IDbConnection CreateConnection()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private void SaveEntry(IDbConnection conn, IDbTransaction tx, AuditEntry entry)
        {
            var insertSql = $@"
                INSERT INTO [{_options.SchemaName}].[{_options.AuditTableName}]
                    (TableName, PrimaryKey, Action, UserName, Timestamp, Metadata, AuditReason, CorrelationId, ChangesJson)
                VALUES (@TableName, @PrimaryKey, @Action, @UserName, @Timestamp, @Metadata, @AuditReason, @CorrelationId, @ChangesJson);
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            entry.Id = conn.ExecuteScalar<long>(insertSql, new
            {
                entry.TableName,
                entry.PrimaryKey,
                Action = (int)entry.Action,
                entry.UserName,
                entry.Timestamp,
                entry.Metadata,
                entry.AuditReason,
                entry.CorrelationId,
                ChangesJson = (_options.StorageMode == AuditStorageMode.SingleTableJson || _options.StorageMode == AuditStorageMode.Both)
                    ? entry.ChangesJson
                    : null
            }, tx);

            if (_options.StorageMode != AuditStorageMode.SingleTableJson && entry.Changes != null && entry.Changes.Count > 0)
            {
                var detailSql = $@"
                    INSERT INTO [{_options.SchemaName}].[{_options.AuditDetailTableName}]
                        (AuditLogId, FieldName, OldValue, NewValue)
                    VALUES (@AuditLogId, @FieldName, @OldValue, @NewValue)";

                conn.Execute(detailSql, entry.Changes.Select(change => new
                {
                    AuditLogId = entry.Id,
                    change.FieldName,
                    change.OldValue,
                    change.NewValue
                }), tx);
            }
        }

        private async Task SaveEntryAsync(IDbConnection conn, IDbTransaction tx, AuditEntry entry)
        {
            var insertSql = $@"
                INSERT INTO [{_options.SchemaName}].[{_options.AuditTableName}]
                    (TableName, PrimaryKey, Action, UserName, Timestamp, Metadata, AuditReason, CorrelationId, ChangesJson)
                VALUES (@TableName, @PrimaryKey, @Action, @UserName, @Timestamp, @Metadata, @AuditReason, @CorrelationId, @ChangesJson);
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            entry.Id = await conn.ExecuteScalarAsync<long>(insertSql, new
            {
                entry.TableName,
                entry.PrimaryKey,
                Action = (int)entry.Action,
                entry.UserName,
                entry.Timestamp,
                entry.Metadata,
                entry.AuditReason,
                entry.CorrelationId,
                ChangesJson = (_options.StorageMode == AuditStorageMode.SingleTableJson || _options.StorageMode == AuditStorageMode.Both)
                    ? entry.ChangesJson
                    : null
            }, tx);

            if (_options.StorageMode != AuditStorageMode.SingleTableJson && entry.Changes != null && entry.Changes.Count > 0)
            {
                var detailSql = $@"
                    INSERT INTO [{_options.SchemaName}].[{_options.AuditDetailTableName}]
                        (AuditLogId, FieldName, OldValue, NewValue)
                    VALUES (@AuditLogId, @FieldName, @OldValue, @NewValue)";

                await conn.ExecuteAsync(detailSql, entry.Changes.Select(change => new
                {
                    AuditLogId = entry.Id,
                    change.FieldName,
                    change.OldValue,
                    change.NewValue
                }), tx);
            }
        }

        private string GetCreateTablesSql()
        {
            return $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{_options.AuditTableName}' AND schema_id = SCHEMA_ID('{_options.SchemaName}'))
                BEGIN
                    CREATE TABLE [{_options.SchemaName}].[{_options.AuditTableName}] (
                        Id             BIGINT IDENTITY(1,1) PRIMARY KEY,
                        TableName      NVARCHAR(256)  NOT NULL,
                        PrimaryKey     NVARCHAR(256)  NOT NULL,
                        Action         INT            NOT NULL,
                        UserName       NVARCHAR(256)  NULL,
                        Timestamp      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                        Metadata       NVARCHAR(MAX)  NULL,
                        AuditReason    NVARCHAR(500)  NULL,
                        CorrelationId  NVARCHAR(128)  NULL,
                        ChangesJson    NVARCHAR(MAX)  NULL
                    );

                    CREATE NONCLUSTERED INDEX IX_{_options.AuditTableName}_Table_PK
                        ON [{_options.SchemaName}].[{_options.AuditTableName}] (TableName, PrimaryKey);

                    CREATE NONCLUSTERED INDEX IX_{_options.AuditTableName}_Timestamp
                        ON [{_options.SchemaName}].[{_options.AuditTableName}] (Timestamp DESC);
                END
                ELSE
                BEGIN
                    -- Schema evolution for existing tables
                    IF COL_LENGTH('[{_options.SchemaName}].[{_options.AuditTableName}]', 'AuditReason') IS NULL
                        ALTER TABLE [{_options.SchemaName}].[{_options.AuditTableName}] ADD AuditReason NVARCHAR(500) NULL;

                    IF COL_LENGTH('[{_options.SchemaName}].[{_options.AuditTableName}]', 'CorrelationId') IS NULL
                        ALTER TABLE [{_options.SchemaName}].[{_options.AuditTableName}] ADD CorrelationId NVARCHAR(128) NULL;

                    IF COL_LENGTH('[{_options.SchemaName}].[{_options.AuditTableName}]', 'ChangesJson') IS NULL
                        ALTER TABLE [{_options.SchemaName}].[{_options.AuditTableName}] ADD ChangesJson NVARCHAR(MAX) NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{_options.AuditDetailTableName}' AND schema_id = SCHEMA_ID('{_options.SchemaName}'))
                BEGIN
                    CREATE TABLE [{_options.SchemaName}].[{_options.AuditDetailTableName}] (
                        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
                        AuditLogId  BIGINT         NOT NULL,
                        FieldName   NVARCHAR(256)  NOT NULL,
                        OldValue    NVARCHAR(MAX)  NULL,
                        NewValue    NVARCHAR(MAX)  NULL,

                        CONSTRAINT FK_{_options.AuditDetailTableName}_{_options.AuditTableName}
                            FOREIGN KEY (AuditLogId)
                            REFERENCES [{_options.SchemaName}].[{_options.AuditTableName}](Id)
                            ON DELETE CASCADE
                    );

                    CREATE NONCLUSTERED INDEX IX_{_options.AuditDetailTableName}_LogId
                        ON [{_options.SchemaName}].[{_options.AuditDetailTableName}] (AuditLogId);
                END;";
        }
    }
}
