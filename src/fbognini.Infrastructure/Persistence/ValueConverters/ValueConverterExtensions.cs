using FastIDs.TypeId;
using fbognini.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Linq;

namespace fbognini.Infrastructure.Persistence.ValueConverters
{
    public static class ValueConverterExtensions
    {
        public static PropertyBuilder<T> HasSerializedJsonConversion<T>(this PropertyBuilder<T> builder)
        {
            return builder.HasConversion<SerializedJsonConverter<T>>();
        }

        public static PropertyBuilder<DateTime> IsUtc(this PropertyBuilder<DateTime> builder)
        {
            return builder
                .HasConversion<UtcDateConverter>();
        }

        public static PropertyBuilder<DateTime?> IsUtc(this PropertyBuilder<DateTime?> builder)
        {
            return builder
                .HasConversion<UtcDateConverter>();
        }

        public static PropertyBuilder<TypeId?> IsTypeId(this PropertyBuilder<TypeId?> builder, string prefix)
        {
            return builder
                    .HasMaxLength(TypeIdLength(prefix))
                    .IsFixedLength()
                    .ValueGeneratedNever()
                    .HasConversion<NullableTypeIdConverter>();
        }

        public static PropertyBuilder<TypeId> IsTypeId(this PropertyBuilder<TypeId> builder, string prefix)
        {
            return builder
                    .HasMaxLength(TypeIdLength(prefix))
                    .IsFixedLength()
                    .IsRequired()
                    .ValueGeneratedNever()
                    .HasConversion<TypeIdConverter>();
        }

        // Suffisso di 26 caratteri, separatore, prefisso.
        private static int TypeIdLength(string prefix) => 26 + 1 + prefix.Length;

        public static ModelBuilder ApplyUtcConverterToAuditableDates(this ModelBuilder builder)
        {
            var auditableDates = builder.Model.GetEntityTypes()
                .Where(entityType => typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
                .SelectMany(entityType => entityType.GetProperties())
                .Where(property => property.ClrType == typeof(DateTime)
                    && property.Name is nameof(IAuditableEntity.CreatedOnUtc) or nameof(IAuditableEntity.LastUpdatedOnUtc) or nameof(IAuditableEntity.LastModifiedOnUtc));

            foreach (var property in auditableDates)
            {
                property.SetValueConverter(new UtcDateConverter());
            }

            return builder;
        }
    }
}
