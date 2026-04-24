using MedInsights.Services;

namespace MedInsights.Authorization.Tests;

public sealed class OpenAIServiceContinuationGuardTests
{
    [Fact]
    public void SelectToolCallKey_RejectsOrphanContinuation_WhenNoContextExists()
    {
        var keyByIndex = new Dictionary<int, string>();
        string? lastToolCallKey = null;

        var key = OpenAIService.SelectToolCallKey(
            index: 0,
            rawId: null,
            rawName: null,
            toolCallKeyByIndex: keyByIndex,
            lastToolCallKey: ref lastToolCallKey,
            rebasedFromKey: out var rebasedFromKey);

        Assert.Null(key);
        Assert.Null(rebasedFromKey);
        Assert.Null(lastToolCallKey);
        Assert.Empty(keyByIndex);
    }

    [Fact]
    public void SelectToolCallKey_UsesIndexKeyForUnnamedIdlessStart_ThenContinuesByIndex()
    {
        var keyByIndex = new Dictionary<int, string>();
        string? lastToolCallKey = null;

        var firstKey = OpenAIService.SelectToolCallKey(
            index: 2,
            rawId: null,
            rawName: "lookup_customer",
            toolCallKeyByIndex: keyByIndex,
            lastToolCallKey: ref lastToolCallKey,
            rebasedFromKey: out var firstRebase);

        var continuationKey = OpenAIService.SelectToolCallKey(
            index: 2,
            rawId: null,
            rawName: null,
            toolCallKeyByIndex: keyByIndex,
            lastToolCallKey: ref lastToolCallKey,
            rebasedFromKey: out var continuationRebase);

        Assert.Equal("idx::2", firstKey);
        Assert.Equal(firstKey, continuationKey);
        Assert.Null(firstRebase);
        Assert.Null(continuationRebase);
        Assert.Equal("idx::2", lastToolCallKey);
    }

    [Fact]
    public void SelectToolCallKey_RebasesIndexKey_WhenStableIdAppears()
    {
        var keyByIndex = new Dictionary<int, string>();
        string? lastToolCallKey = null;

        var firstKey = OpenAIService.SelectToolCallKey(
            index: 1,
            rawId: null,
            rawName: "create_invoice",
            toolCallKeyByIndex: keyByIndex,
            lastToolCallKey: ref lastToolCallKey,
            rebasedFromKey: out _);

        var canonicalKey = OpenAIService.SelectToolCallKey(
            index: 1,
            rawId: "call_123",
            rawName: null,
            toolCallKeyByIndex: keyByIndex,
            lastToolCallKey: ref lastToolCallKey,
            rebasedFromKey: out var rebasedFromKey);

        Assert.Equal("idx::1", firstKey);
        Assert.Equal("call_123", canonicalKey);
        Assert.Equal("idx::1", rebasedFromKey);
        Assert.Equal("call_123", keyByIndex[1]);
        Assert.Equal("call_123", lastToolCallKey);
    }
}
