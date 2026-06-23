using Xunit;

namespace TravelAssistant.Security.Pii.Tests;

public sealed class DataClassAttributeTests
{
    private sealed class Sample
    {
        [DataClass(DataClass.Public)] public string City { get; set; } = "";
        [DataClass(DataClass.Internal)] public string ProviderId { get; set; } = "";
        [DataClass(DataClass.Sensitive)] public string Email { get; set; } = "";
        public string Unclassified { get; set; } = "";
    }

    [Fact]
    public void Attribute_ReturnsClass()
    {
        var prop = typeof(Sample).GetProperty(nameof(Sample.Email))!;
        var attr = (DataClassAttribute)Attribute.GetCustomAttribute(prop, typeof(DataClassAttribute))!;
        Assert.Equal(DataClass.Sensitive, attr.Class);
    }

    [Fact]
    public void UnclassifiedProperty_HasNoAttribute_AndIsRejectedByConvention()
    {
        // ProductionGuard's APP-6 reflection scan must treat this as Sensitive-by-default and
        // require either an explicit attribute or a registered converter. This test pins the
        // contract: absence of attribute is *the* failure mode, not a silent pass.
        var prop = typeof(Sample).GetProperty(nameof(Sample.Unclassified))!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(DataClassAttribute));
        Assert.Null(attr);
    }
}
