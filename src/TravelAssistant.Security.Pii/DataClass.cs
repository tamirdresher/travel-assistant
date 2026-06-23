namespace TravelAssistant.Security.Pii;

/// <summary>
/// Classification level for a data property. Drives whether encryption is required.
/// Default for any string-bearing property without an explicit attribute is <see cref="Sensitive"/>
/// (deny-by-default), enforced by the ProductionGuard reflection scan.
/// </summary>
public enum DataClass
{
    /// <summary>
    /// Public information. No encryption required. Examples: city names, airport codes, currency.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Internal information. Not user-identifying. No encryption required at rest, but not exposed
    /// in unauthenticated APIs. Examples: provider IDs, internal IDs, request IDs.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Sensitive / PII. Encryption at rest is REQUIRED via <see cref="IPiiCipher"/>.
    /// Examples: traveler names, email, phone, passport numbers, free-text travel preferences.
    /// </summary>
    Sensitive = 2,
}

/// <summary>
/// Marks a property's data classification. Used by EF Core / Cosmos value converters to decide
/// whether the property must round-trip through <see cref="IPiiCipher"/>, and by ProductionGuard's
/// reflection scan to fail-fast if a string property lacks classification.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DataClassAttribute : Attribute
{
    /// <summary>Classification level for the annotated property.</summary>
    public DataClass Class { get; }

    /// <summary>Create a classification attribute.</summary>
    public DataClassAttribute(DataClass @class) => Class = @class;
}
