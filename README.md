# 🔍 AuditTrail — Automatic Change Tracking for .NET

> **Track who changed what, when, and what the old/new values were — zero config required.**

[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

AuditTrail provides **automatic change detection and audit logging** for any .NET application.
Works with WinForms, ASP.NET, WebAPI, and any .NET Standard 2.0 compatible project.

---

## 📦 Features

| Feature | Description |
|---------|-------------|
| **Compiled Accessors** | High-performance Expression Tree getters (zero Reflection overhead) |
| **Fast Value Equality** | Direct type comparisons without intermediate string allocations |
| **Auto Metadata Resolution** | Automatic table name & PK inference (`[Table]`, `[Key]`, `Id`, `{Type}Id`) |
| **Flexible Storage Modes** | `NormalizedTables`, high-throughput `SingleTableJson`, or `Both` |
| **Transaction Context** | `AuditReason` and `CorrelationId` for tracing business workflows |
| **Auto Change Detection** | High-speed diff between entity snapshots or instances |
| **CRUD Tracking** | Insert, Update, Delete with old/new values |
| **Field-level Changes** | Each changed field recorded separately |
| **Table/Field Filtering** | Include/exclude specific tables or fields |
| **SQL Server Storage** | Auto-creates tables with auto schema migration |
| **In-Memory Store** | For testing without database |
| **Batch Operations** | Multiple audit entries saved in a single transaction |
| **Async Support** | `SaveChangesAsync()` for non-blocking writes |
| **Zero Config** | Works out of the box, customizable via `AuditOptions` |

---

## 🚀 Quick Start

### Auto-Track Updates (Zero Boilerplate)

```csharp
var store = new SqlServerAuditStore(connectionString);
store.EnsureCreated(); // creates/updates tables automatically

var audit = new AuditContext(store, currentUser)
{
    CurrentAuditReason = "Customer requested phone number update",
    CurrentCorrelationId = "REQ-2026-0042"
};

// Auto detects table "Products" and primary key "product.Id"
audit.TrackUpdate(originalProduct, modifiedProduct);
audit.SaveChanges();
```

### Track with Explicit Metadata & Snapshots

```csharp
// Take snapshot before modification
var snapshot = audit.Snapshot(product);

// Modify the entity
product.Price = 999;
product.Name = "Updated Widget";

// Track and save
audit.TrackUpdate(snapshot, product, "Products", product.Id.ToString());
audit.SaveChanges();
```

### Track Insert/Delete

```csharp
// Insert (Auto-inferred PK and table name)
audit.TrackInsert(newProduct);

// Delete (Auto-inferred PK and table name)
audit.TrackDelete(deletedProduct);

// Batch save
audit.SaveChanges();
```

### High-Throughput Single-Table Mode

```csharp
var options = new AuditOptions
{
    StorageMode = AuditStorageMode.SingleTableJson // Store changes as JSON in AuditLog.ChangesJson
};
var audit = new AuditContext(store, currentUser, options);
```

### Query Audit History

```csharp
// Get all changes for a specific record
var history = audit.GetHistory("Products", "42");
foreach (var entry in history)
{
    Console.WriteLine($"[{entry.Timestamp}] {entry.Action} by {entry.UserName} (Reason: {entry.AuditReason})");
    foreach (var change in entry.Changes)
    {
        Console.WriteLine($"  {change.FieldName}: {change.OldValue} → {change.NewValue}");
    }
}
```

### Configure Filtering

```csharp
var options = new AuditOptions
{
    ExcludeFields = { "PasswordHash", "Users.SecretKey" },
    ExcludeTables = { "TempTable", "Logs" },
    MaxValueLength = 2000,
    StorageMode = AuditStorageMode.NormalizedTables // or SingleTableJson, Both
};

var audit = new AuditContext(store, currentUser, options);
```

---

## 📖 API Reference

### AuditContext (Main Entry Point)

```csharp
TrackInsert<T>(entity)                                  // auto-infer table & PK
TrackInsert<T>(entity, table, pk)                      // track new record explicitly
TrackDelete<T>(entity)                                  // auto-infer table & PK
TrackDelete<T>(entity, table, pk)                      // track deleted record explicitly
TrackUpdate<T>(snapshot, current)                       // auto-infer table & PK
TrackUpdate<T>(original, modified)                      // auto-infer table & PK
TrackUpdate(snapshot, current, table, pk)               // track via snapshot
TrackUpdate(original, modified, table, pk)              // track via comparison
Snapshot<T>(entity)                                     // create snapshot
SaveChanges() / SaveChangesAsync()                     // persist to store
GetHistory(table, pk)                                   // query history
DiscardChanges()                                        // clear pending
```

### AuditOptions

```csharp
StorageMode         // NormalizedTables (default), SingleTableJson, or Both
UseCompiledAccessors// Compile property getters with Expression trees (default: true)
EnableFastEquality  // Fast type-specific equality comparison without strings (default: true)
IncludeTables       // whitelist (empty = audit all)
ExcludeTables       // blacklist
ExcludeFields       // "FieldName" or "Table.FieldName"
TrackDeletedValues  // record old values on delete (default: true)
TrackInsertedValues // record new values on insert (default: true)
MaxValueLength      // truncate long values (default: 4000)
```

### Storage Backends

| Store | Use Case |
|-------|----------|
| `SqlServerAuditStore` | Production (SQL Server) with auto-migration |
| `InMemoryAuditStore` | Unit testing |
| Custom `IAuditStore` | Any backend (MongoDB, file, etc.) |

---

## 🗄️ Database Schema

Auto-created and migrated by `EnsureCreated()`:

```
AuditLog (Header)
├── Id (BIGINT PK)
├── TableName (NVARCHAR 256)
├── PrimaryKey (NVARCHAR 256)
├── Action (INT: 1=Insert, 2=Update, 3=Delete)
├── UserName (NVARCHAR 256)
├── Timestamp (DATETIME2)
├── AuditReason (NVARCHAR 500)
├── CorrelationId (NVARCHAR 100)
├── ChangesJson (NVARCHAR MAX)
└── Metadata (NVARCHAR MAX)

AuditLogDetail (Field Changes - Optional in SingleTableJson mode)
├── Id (BIGINT PK)
├── AuditLogId (FK → AuditLog)
├── FieldName (NVARCHAR 256)
├── OldValue (NVARCHAR MAX)
└── NewValue (NVARCHAR MAX)
```

---

## 📄 License

Apache License 2.0

---

<p align="center">
  <strong>🔍 Know who changed what.</strong>
</p>
