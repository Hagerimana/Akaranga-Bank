using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AkarangaBankMvc.Web.Data.Conversions;

/// <summary>
/// Normalise les <see cref="DateTime"/> en UTC avant écriture PostgreSQL.
/// Npgsql mappe DateTime sur "timestamp with time zone" et refuse les
/// valeurs dont le Kind n'est pas Utc (cas des dates saisies au formulaire).
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => VersUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    internal static DateTime VersUtc(DateTime valeur) => valeur.Kind switch
    {
        DateTimeKind.Utc => valeur,
        DateTimeKind.Local => valeur.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valeur, DateTimeKind.Utc)
    };
}

/// <summary>
/// Variante nullable de <see cref="UtcDateTimeConverter"/>.
/// </summary>
public sealed class NullableUtcDateTimeConverter
    : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue ? UtcDateTimeConverter.VersUtc(v.Value) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
