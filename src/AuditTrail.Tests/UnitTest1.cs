using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AuditTrail.Tests
{
    // ─── Test Entities ────────────────────────────────────────────

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
    }

    // ─── ChangeTracker Tests ──────────────────────────────────────

    public class ChangeTrackerTests
    {
        [Fact]
        public void DetectChanges_UpdatedFields_ReturnsChanges()
        {
            var original = new Product { Id = 1, Name = "Widget", Price = 10.00m, Stock = 100 };
            var modified = new Product { Id = 1, Name = "Widget", Price = 15.00m, Stock = 80 };

            var entry = ChangeTracker.DetectChanges(original, modified, "Products", "1", "admin");

            Assert.NotNull(entry);
            Assert.Equal(AuditAction.Update, entry.Action);
            Assert.Equal("Products", entry.TableName);
            Assert.Equal("1", entry.PrimaryKey);
            Assert.Equal("admin", entry.UserName);
            Assert.Equal(2, entry.Changes.Count); // Price + Stock changed

            var priceChange = entry.Changes.First(c => c.FieldName == "Price");
            Assert.Equal("10.00", priceChange.OldValue);
            Assert.Equal("15.00", priceChange.NewValue);
        }

        [Fact]
        public void DetectChanges_NoChanges_ReturnsNull()
        {
            var original = new Product { Id = 1, Name = "Widget", Price = 10.00m };
            var modified = new Product { Id = 1, Name = "Widget", Price = 10.00m };

            var entry = ChangeTracker.DetectChanges(original, modified, "Products", "1", "admin");

            Assert.Null(entry); // no changes detected
        }

        [Fact]
        public void TrackInsert_RecordsAllFields()
        {
            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m, Stock = 50 };

            var entry = ChangeTracker.TrackInsert(product, "Products", "1", "admin");

            Assert.NotNull(entry);
            Assert.Equal(AuditAction.Insert, entry.Action);
            Assert.True(entry.Changes.Count >= 3); // at least Name, Price, Stock
            Assert.All(entry.Changes, c => Assert.Null(c.OldValue));
        }

        [Fact]
        public void TrackDelete_RecordsAllFields()
        {
            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m, Stock = 50 };

            var entry = ChangeTracker.TrackDelete(product, "Products", "1", "admin");

            Assert.NotNull(entry);
            Assert.Equal(AuditAction.Delete, entry.Action);
            Assert.True(entry.Changes.Count >= 3);
            Assert.All(entry.Changes, c => Assert.Null(c.NewValue));
        }

        [Fact]
        public void Snapshot_AndDetectChanges_Works()
        {
            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m };
            var snapshot = ChangeTracker.Snapshot(product);

            // Simulate modification
            product.Price = 20.00m;
            product.Name = "Super Widget";

            var entry = ChangeTracker.DetectChangesFromSnapshot(
                snapshot, product, "Products", "1", "admin");

            Assert.NotNull(entry);
            Assert.Equal(2, entry.Changes.Count);
        }

        [Fact]
        public void ExcludeFields_FiltersCorrectly()
        {
            var options = new AuditOptions();
            options.ExcludeFields.Add("PasswordHash");

            var original = new User { Id = 1, UserName = "john", PasswordHash = "old" };
            var modified = new User { Id = 1, UserName = "john_updated", PasswordHash = "new" };

            var entry = ChangeTracker.DetectChanges(original, modified, "Users", "1", "admin", options);

            Assert.NotNull(entry);
            Assert.DoesNotContain(entry.Changes, c => c.FieldName == "PasswordHash");
            Assert.Contains(entry.Changes, c => c.FieldName == "UserName");
        }

        [Fact]
        public void ExcludeTable_ReturnsNull()
        {
            var options = new AuditOptions();
            options.ExcludeTables.Add("TempTable");

            var entry = ChangeTracker.DetectChanges(
                new Product { Name = "A" },
                new Product { Name = "B" },
                "TempTable", "1", "admin", options);

            Assert.Null(entry);
        }

        [Fact]
        public void IncludeTablesFilter_OnlyAuditsListed()
        {
            var options = new AuditOptions();
            options.IncludeTables.Add("Products");

            // Products should be audited
            var entry1 = ChangeTracker.DetectChanges(
                new Product { Name = "A" }, new Product { Name = "B" },
                "Products", "1", "admin", options);
            Assert.NotNull(entry1);

            // Users should NOT be audited (not in include list)
            var entry2 = ChangeTracker.DetectChanges(
                new User { UserName = "A" }, new User { UserName = "B" },
                "Users", "1", "admin", options);
            Assert.Null(entry2);
        }

        [Fact]
        public void DateTime_FormattedAsISO8601()
        {
            var dt = new DateTime(2024, 12, 25, 10, 30, 0, DateTimeKind.Utc);
            var original = new Product { ModifiedDate = null };
            var modified = new Product { ModifiedDate = dt };

            var entry = ChangeTracker.DetectChanges(original, modified, "Products", "1", "admin");

            Assert.NotNull(entry);
            var change = entry.Changes.First(c => c.FieldName == "ModifiedDate");
            Assert.Contains("2024-12-25", change.NewValue);
        }
    }

    // ─── AuditOptions Tests ───────────────────────────────────────

    public class AuditOptionsTests
    {
        [Fact]
        public void ShouldAuditTable_ExcludeWorks()
        {
            var options = new AuditOptions();
            options.ExcludeTables.Add("Logs");

            Assert.False(options.ShouldAuditTable("Logs"));
            Assert.True(options.ShouldAuditTable("Products"));
        }

        [Fact]
        public void ShouldAuditField_TableSpecificExclude()
        {
            var options = new AuditOptions();
            options.ExcludeFields.Add("Users.PasswordHash");

            Assert.False(options.ShouldAuditField("Users", "PasswordHash"));
            Assert.True(options.ShouldAuditField("Products", "PasswordHash")); // different table
        }

        [Fact]
        public void ShouldAuditField_GlobalExclude()
        {
            var options = new AuditOptions();
            options.ExcludeFields.Add("PasswordHash");

            Assert.False(options.ShouldAuditField("Users", "PasswordHash"));
            Assert.False(options.ShouldAuditField("Admins", "PasswordHash"));
        }
    }

    // ─── AuditContext Tests ───────────────────────────────────────

    public class AuditContextTests
    {
        [Fact]
        public void TrackInsert_AndSave_PersistsToStore()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m };
            audit.TrackInsert(product, "Products", "1");
            audit.SaveChanges();

            Assert.Single(store.Entries);
            Assert.Equal(AuditAction.Insert, store.Entries[0].Action);
        }

        [Fact]
        public void TrackUpdate_WithSnapshot_DetectsChanges()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m };
            var snapshot = audit.Snapshot(product);

            product.Price = 25.00m;

            audit.TrackUpdate(snapshot, product, "Products", "1");
            audit.SaveChanges();

            Assert.Single(store.Entries);
            Assert.Equal(AuditAction.Update, store.Entries[0].Action);
            Assert.Contains(store.Entries[0].Changes, c => c.FieldName == "Price");
        }

        [Fact]
        public void TrackDelete_RecordsOldValues()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            var product = new Product { Id = 1, Name = "Widget", Price = 10.00m };
            audit.TrackDelete(product, "Products", "1");
            audit.SaveChanges();

            Assert.Single(store.Entries);
            Assert.Equal(AuditAction.Delete, store.Entries[0].Action);
        }

        [Fact]
        public void MultipleTracks_BatchSave()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            audit.TrackInsert(new Product { Id = 1, Name = "A" }, "Products", "1");
            audit.TrackInsert(new Product { Id = 2, Name = "B" }, "Products", "2");
            audit.TrackDelete(new Product { Id = 3, Name = "C" }, "Products", "3");

            Assert.Equal(3, audit.PendingCount);
            audit.SaveChanges();

            Assert.Equal(3, store.Entries.Count);
            Assert.Equal(0, audit.PendingCount);
        }

        [Fact]
        public void DiscardChanges_ClearsPending()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            audit.TrackInsert(new Product { Id = 1, Name = "A" }, "Products", "1");
            Assert.Equal(1, audit.PendingCount);

            audit.DiscardChanges();
            Assert.Equal(0, audit.PendingCount);

            audit.SaveChanges();
            Assert.Empty(store.Entries);
        }

        [Fact]
        public void GetHistory_ReturnsCorrectRecords()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            audit.TrackInsert(new Product { Id = 1, Name = "A" }, "Products", "1");
            audit.TrackInsert(new Product { Id = 2, Name = "B" }, "Products", "2");
            audit.SaveChanges();

            var history = audit.GetHistory("Products", "1").ToList();
            Assert.Single(history);
            Assert.Equal("1", history[0].PrimaryKey);
        }


        [Fact]
        public async System.Threading.Tasks.Task SaveChangesAsync_Works()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            audit.TrackInsert(new Product { Id = 1, Name = "Widget" }, "Products", "1");
            await audit.SaveChangesAsync();

            Assert.Single(store.Entries);
        }

        [Fact]
        public void TrackUpdate_TwoEntities_CompareDirectly()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin");

            var original = new Product { Id = 1, Name = "Old", Price = 10.00m };
            var modified = new Product { Id = 1, Name = "New", Price = 20.00m };

            audit.TrackUpdate(original, modified, "Products", "1");
            audit.SaveChanges();

            Assert.Single(store.Entries);
            Assert.Equal(2, store.Entries[0].Changes.Count);
        }

        [Fact]
        public void AutoMetadata_InfersTableNameAndPrimaryKey()
        {
            var store = new InMemoryAuditStore();
            var audit = new AuditContext(store, "admin")
            {
                CurrentAuditReason = "Price adjustment",
                CurrentCorrelationId = "TXN-9999"
            };

            var original = new Product { Id = 42, Name = "Laptop", Price = 1000m };
            var modified = new Product { Id = 42, Name = "Laptop Pro", Price = 1200m };

            // Pure auto-detection overload: no tableName or primaryKey string specified!
            audit.TrackUpdate(original, modified);
            audit.SaveChanges();

            Assert.Single(store.Entries);
            var entry = store.Entries[0];
            Assert.Equal("Product", entry.TableName);
            Assert.Equal("42", entry.PrimaryKey);
            Assert.Equal("Price adjustment", entry.AuditReason);
            Assert.Equal("TXN-9999", entry.CorrelationId);
            Assert.Equal(2, entry.Changes.Count);

            // Test SingleTableJson ChangesJson generation
            Assert.NotNull(entry.ChangesJson);
            Assert.Contains("\"Field\":\"Name\"", entry.ChangesJson);
            Assert.Contains("\"Field\":\"Price\"", entry.ChangesJson);
        }

        [Fact]
        public void FastEquals_SameDecimalRepresentations_DetectedAsUnchanged()
        {
            // 10.0m and 10.000m have different trailing zeros in string ToString()
            // but FastEquals should recognize decimal equality without marking as changed.
            var original = new Product { Id = 1, Name = "Item", Price = 10.0m };
            var modified = new Product { Id = 1, Name = "Item", Price = 10.000m };

            var entry = ChangeTracker.DetectChanges(original, modified, "Products", "1", "admin");
            Assert.Null(entry);
        }
    }

    // ─── InMemoryAuditStore Tests ─────────────────────────────────

    public class InMemoryAuditStoreTests
    {
        [Fact]
        public void GetTableHistory_ReturnsOrderedByTimestamp()
        {
            var store = new InMemoryAuditStore();

            store.Save(new AuditEntry
            {
                TableName = "Products",
                PrimaryKey = "1",
                Action = AuditAction.Insert,
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                Changes = new List<AuditFieldChange>()
            });
            store.Save(new AuditEntry
            {
                TableName = "Products",
                PrimaryKey = "2",
                Action = AuditAction.Insert,
                Timestamp = DateTime.UtcNow,
                Changes = new List<AuditFieldChange>()
            });

            var history = store.GetTableHistory("Products").ToList();
            Assert.Equal(2, history.Count);
            Assert.True(history[0].Timestamp >= history[1].Timestamp);
        }
    }
}
