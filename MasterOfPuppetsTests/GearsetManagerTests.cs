using System.Linq;

using Xunit;

using MasterOfPuppets;

public sealed class GearsetManagerTests {
    private static readonly GearsetDescriptor[] Gearsets = [
        new(2, "Raid Paladin", 19),
        new(7, "Performance Paladin", 19),
        new(9, "Performance Paladin", 19),
        new(12, "Raid Warrior", 21),
    ];

    [Fact]
    public void Resolve_WithoutSelectorRejectsImplicitSelection() {
        var result = GearsetManager.ResolveGearset(Gearsets, 21, null);

        Assert.False(result.Success);
        Assert.Equal("selector_required", result.Status);
        Assert.Contains("requires an exact gearset name or number", result.Message);
    }

    [Fact]
    public void Resolve_NumberSelectsExactGearsetAndValidatesJob() {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByNumber(7));

        Assert.True(result.Success);
        Assert.Equal(7, result.Gearset!.Number);
        Assert.Equal("Performance Paladin", result.Gearset.Name);
    }

    [Fact]
    public void Resolve_NumberRejectsWrongJob() {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByNumber(12));

        Assert.False(result.Success);
        Assert.Equal("job_mismatch", result.Status);
        Assert.Single(result.Candidates);
    }

    [Fact]
    public void Resolve_NameIsExactCaseInsensitiveAndJobScoped() {
        var result = GearsetManager.ResolveGearset(Gearsets, 21, GearsetSelector.ByName("raid warrior"));

        Assert.True(result.Success);
        Assert.Equal(12, result.Gearset!.Number);
    }

    [Fact]
    public void Resolve_DuplicateNameIsAmbiguous() {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByName("Performance Paladin"));

        Assert.False(result.Success);
        Assert.Equal("ambiguous", result.Status);
        Assert.Equal([7, 9], result.Candidates.Select(item => item.Number));
    }

    [Fact]
    public void Resolve_NameRejectsWrongJobInsteadOfEquippingIt() {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByName("Raid Warrior"));

        Assert.False(result.Success);
        Assert.Equal("job_mismatch", result.Status);
        Assert.Equal(12, Assert.Single(result.Candidates).Number);
    }

    [Fact]
    public void Resolve_MissingSelectorReturnsMissing() {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByNumber(99));

        Assert.False(result.Success);
        Assert.Equal("missing", result.Status);
        Assert.Empty(result.Candidates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Resolve_InvalidGearsetNumberIsRejected(int number) {
        var result = GearsetManager.ResolveGearset(Gearsets, 19, GearsetSelector.ByNumber(number));

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
    }
}
