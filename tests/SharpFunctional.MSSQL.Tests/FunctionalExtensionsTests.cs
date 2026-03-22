using LanguageExt;
using LanguageExt.Common;
using SharpFunctional.MsSql.Common;
using Xunit;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.Tests;

public class FunctionalExtensionsTests
{
    // --- Bind Option<TIn> -> Option<TOut> ---

    [Fact]
    public async Task Bind_Option_WithSomeValue_ShouldReturnMappedSome()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.Some(5));

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Option<string>.Some($"value:{v}")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        result.IfSome(v => Assert.Equal("value:5", v));
    }

    [Fact]
    public async Task Bind_Option_WithNone_ShouldReturnNone()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.None);

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Option<string>.Some($"value:{v}")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task Bind_Option_WithNullSource_ShouldReturnNone()
    {
        // Arrange
        Task<Option<int>> source = null!;

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Option<string>.Some($"value:{v}")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task Bind_Option_WithNullBinder_ShouldReturnNone()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.Some(5));

        // Act
        var result = await FunctionalExtensions.Bind(source, (Func<int, Task<Option<string>>>)null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    // --- Bind Option<TIn> -> Seq<TOut> ---

    [Fact]
    public async Task Bind_OptionToSeq_WithSomeValue_ShouldReturnSeq()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.Some(3));

        // Act
        var result = await FunctionalExtensions.Bind(source, v =>
            Task.FromResult(toSeq(Enumerable.Range(1, v).ToList())), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Bind_OptionToSeq_WithNone_ShouldReturnEmptySeq()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.None);

        // Act
        var result = await FunctionalExtensions.Bind(source, v =>
            Task.FromResult(toSeq(Enumerable.Range(1, v).ToList())), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task Bind_OptionToSeq_WithNullBinder_ShouldReturnEmptySeq()
    {
        // Arrange
        var source = Task.FromResult(Option<int>.Some(3));

        // Act
        var result = await FunctionalExtensions.Bind(source, (Func<int, Task<Seq<string>>>)null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    // --- Map Seq ---

    [Fact]
    public async Task Map_Seq_WithValues_ShouldApplyMapper()
    {
        // Arrange
        var source = Task.FromResult(toSeq(new List<int> { 1, 2, 3 }));

        // Act
        var result = await FunctionalExtensions.Map(source, seq => seq.Map(x => x * 10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(10, result[0]);
        Assert.Equal(20, result[1]);
        Assert.Equal(30, result[2]);
    }

    [Fact]
    public async Task Map_Seq_WithNullMapper_ShouldReturnEmptySeq()
    {
        // Arrange
        var source = Task.FromResult(toSeq(new List<int> { 1, 2, 3 }));

        // Act
        var result = await FunctionalExtensions.Map(source, (Func<Seq<int>, Seq<int>>)null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    // --- Bind Fin<TIn> -> Fin<TOut> ---

    [Fact]
    public async Task Bind_Fin_WithSuccess_ShouldReturnMappedSuccess()
    {
        // Arrange
        var source = Task.FromResult(Fin<int>.Succ(42));

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Fin<string>.Succ($"answer:{v}")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(v => Assert.Equal("answer:42", v));
    }

    [Fact]
    public async Task Bind_Fin_WithFail_ShouldPropagateError()
    {
        // Arrange
        var error = Error.New("something failed");
        var source = Task.FromResult(FinFail<int>(error));

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Fin<string>.Succ($"answer:{v}")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task Bind_Fin_WithCanceledToken_ShouldReturnFail()
    {
        // Arrange
        var source = Task.Run(async () =>
        {
            await Task.Delay(200);
            return Fin<int>.Succ(42);
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await FunctionalExtensions.Bind(source, v => Task.FromResult(Fin<string>.Succ($"answer:{v}")), cts.Token);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task Bind_Fin_WithNullBinder_ShouldReturnFail()
    {
        // Arrange
        var source = Task.FromResult(Fin<int>.Succ(42));

        // Act
        var result = await FunctionalExtensions.Bind(source, (Func<int, Task<Fin<string>>>)null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }
}
