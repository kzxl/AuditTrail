using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AuditTrail
{
    /// <summary>
    /// High-performance compiled property accessor for audit entities.
    /// Eliminates reflection overhead by using compiled lambda expressions.
    /// </summary>
    public sealed class AuditPropertyAccessor
    {
        public PropertyInfo Property { get; }
        public string Name { get; }
        public Type PropertyType { get; }
        public bool IsSimpleType { get; }
        public bool IsPrimaryKey { get; }
        public Func<object, object> Getter { get; }

        public AuditPropertyAccessor(PropertyInfo property, Type entityType)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Name = property.Name;
            PropertyType = property.PropertyType;
            IsSimpleType = CheckIsSimpleType(property.PropertyType);
            IsPrimaryKey = CheckIsPrimaryKey(property, entityType);
            Getter = CompileGetter(property, entityType);
        }

        private static Func<object, object> CompileGetter(PropertyInfo property, Type entityType)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instanceParam, entityType);
            var propertyAccess = Expression.Property(typedInstance, property);
            var convertResult = Expression.Convert(propertyAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(convertResult, instanceParam);
            return lambda.Compile();
        }

        private static bool CheckIsPrimaryKey(PropertyInfo property, Type entityType)
        {
            // 1. Check for [Key] attribute (from DataAnnotations or custom)
            foreach (var attr in property.GetCustomAttributes(true))
            {
                var attrType = attr.GetType();
                if (attrType.Name.Equals("KeyAttribute", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (attrType.Name.Equals("ColumnAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    var isPkProp = attrType.GetProperty("IsPrimaryKey");
                    if (isPkProp != null && isPkProp.PropertyType == typeof(bool))
                    {
                        var val = isPkProp.GetValue(attr, null);
                        if (val is bool isPk && isPk)
                            return true;
                    }
                }
            }

            // 2. Fallback naming convention: "Id", "ID", or "{EntityType}Id"
            if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(property.Name, $"{entityType.Name}Id", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool CheckIsSimpleType(Type type)
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
    }

    /// <summary>
    /// Thread-safe cached metadata for audit entities.
    /// Analyzes entity structure once and caches compiled property getters and table names.
    /// </summary>
    public sealed class AuditTypeMetadata
    {
        private static readonly ConcurrentDictionary<Type, AuditTypeMetadata> _cache =
            new ConcurrentDictionary<Type, AuditTypeMetadata>();

        private readonly Dictionary<string, AuditPropertyAccessor> _propertyLookup;

        public Type EntityType { get; }
        public string DefaultTableName { get; }
        public AuditPropertyAccessor PrimaryKeyAccessor { get; }
        public IReadOnlyList<AuditPropertyAccessor> AuditableProperties { get; }

        public AuditTypeMetadata(Type entityType)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            DefaultTableName = ResolveTableName(entityType);

            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Select(p => new AuditPropertyAccessor(p, entityType))
                .Where(p => p.IsSimpleType)
                .ToList();

            AuditableProperties = properties.AsReadOnly();
            _propertyLookup = properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            PrimaryKeyAccessor = properties.FirstOrDefault(p => p.IsPrimaryKey);
        }

        public AuditPropertyAccessor GetProperty(string name)
        {
            _propertyLookup.TryGetValue(name, out var accessor);
            return accessor;
        }

        public string ResolvePrimaryKey(object entity)
        {
            if (entity == null || PrimaryKeyAccessor == null) return null;
            var val = PrimaryKeyAccessor.Getter(entity);
            return val?.ToString();
        }

        public static AuditTypeMetadata Get(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return _cache.GetOrAdd(entityType, t => new AuditTypeMetadata(t));
        }

        public static AuditTypeMetadata Get<T>() where T : class => Get(typeof(T));

        private static string ResolveTableName(Type type)
        {
            foreach (var attr in type.GetCustomAttributes(true))
            {
                var attrType = attr.GetType();
                if (attrType.Name.Equals("TableAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    var nameProp = attrType.GetProperty("Name");
                    if (nameProp != null)
                    {
                        var val = nameProp.GetValue(attr, null) as string;
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }
            }
            return type.Name;
        }
    }
}
