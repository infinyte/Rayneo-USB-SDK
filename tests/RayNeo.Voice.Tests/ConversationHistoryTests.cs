// -----------------------------------------------------------------------------
// ConversationHistoryTests.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class ConversationHistoryTests
{
    [Fact]
    public void NewHistory_IsEmpty()
    {
        var history = new ConversationHistory();
        Assert.Equal(0, history.Count);
        Assert.Empty(history.Turns);
    }

    [Fact]
    public void AddsTurns_InOrder_WithRoles()
    {
        var history = new ConversationHistory();
        history.AddUserTurn("what time is it");
        history.AddAssistantTurn("It is nine o'clock.");
        history.AddUserTurn("thanks");

        Assert.Equal(3, history.Count);
        Assert.Equal(ConversationRole.User, history.Turns[0].Role);
        Assert.Equal("what time is it", history.Turns[0].Text);
        Assert.Equal(ConversationRole.Assistant, history.Turns[1].Role);
        Assert.Equal("It is nine o'clock.", history.Turns[1].Text);
        Assert.Equal(ConversationRole.User, history.Turns[2].Role);
    }

    [Fact]
    public void TrimsSurroundingWhitespace()
    {
        var history = new ConversationHistory();
        history.AddUserTurn("  hello there  ");
        Assert.Equal("hello there", history.Turns[0].Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void RejectsEmptyOrWhitespaceTurns(string? text)
    {
        var history = new ConversationHistory();
        Assert.Throws<ArgumentException>(() => history.AddUserTurn(text!));
        Assert.Throws<ArgumentException>(() => history.AddAssistantTurn(text!));
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void Clear_RemovesAllTurns()
    {
        var history = new ConversationHistory();
        history.AddUserTurn("one");
        history.AddAssistantTurn("two");

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Empty(history.Turns);
    }

    [Fact]
    public void Changed_FiresOnAdd_AndOnClearWhenNonEmpty()
    {
        var history = new ConversationHistory();
        int changes = 0;
        history.Changed += (_, _) => changes++;

        history.AddUserTurn("a");        // 1
        history.AddAssistantTurn("b");   // 2
        history.Clear();                 // 3

        Assert.Equal(3, changes);
    }

    [Fact]
    public void Clear_OnEmptyHistory_DoesNotRaiseChanged()
    {
        var history = new ConversationHistory();
        bool raised = false;
        history.Changed += (_, _) => raised = true;

        history.Clear();

        Assert.False(raised);
    }

    [Fact]
    public void Turns_IsASnapshotThatDoesNotExposeInternalList()
    {
        var history = new ConversationHistory();
        history.AddUserTurn("first");

        var snapshot = history.Turns;
        history.AddUserTurn("second");

        // The earlier snapshot reflects the state at the time it was taken.
        Assert.Single(snapshot);
        Assert.Equal(2, history.Count);
    }
}
