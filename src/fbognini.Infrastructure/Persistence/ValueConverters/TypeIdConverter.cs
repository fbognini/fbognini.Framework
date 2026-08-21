using FastIDs.TypeId;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace fbognini.Infrastructure.Persistence.ValueConverters
{
    public class NullableTypeIdConverter : ValueConverter<TypeId?, string?>
    {
        public NullableTypeIdConverter()
            : base(
                v => v.HasValue ? v.ToString() : null,
                v => !string.IsNullOrWhiteSpace(v) ? TypeId.Parse(v) : null)
        {
        }
    }

    public class TypeIdConverter : ValueConverter<TypeId, string>
    {
        public TypeIdConverter()
            : base(
                v => v!.ToString()!,
                v => TypeId.Parse(v))
        {
        }
    }
}
