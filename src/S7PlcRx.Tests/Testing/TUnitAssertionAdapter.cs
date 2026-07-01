// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using TUnit.Assertions.Extensions;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests.Testing;

internal static class Assert
{
    public static void That<TActual>(TActual actual, Constraint constraint, string? message = null) =>
        constraint.Apply(actual, message);

    public static void That(Func<Task> action, ThrowsConstraint constraint, string? message = null) =>
        constraint.Apply(action, message);

    public static void Multiple(Action action) => action();

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AssertionHelpers.AssertTrue(ex is TException, $"Expected exception assignable to {typeof(TException).FullName}, but got {ex.GetType().FullName}.");
            return (TException)ex;
        }

        AssertionHelpers.AssertTrue(false, $"Expected exception assignable to {typeof(TException).FullName}, but no exception was thrown.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    public static TException ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            action().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AssertionHelpers.AssertTrue(ex is TException, $"Expected exception assignable to {typeof(TException).FullName}, but got {ex.GetType().FullName}.");
            return (TException)ex;
        }

        AssertionHelpers.AssertTrue(false, $"Expected exception assignable to {typeof(TException).FullName}, but no exception was thrown.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    public static void DoesNotThrow(Action action, string? message = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AssertionHelpers.AssertTrue(false, message ?? $"Expected no exception, but got {ex.GetType().FullName}: {ex.Message}");
        }
    }

    public static void Pass(string? message = null) => AssertionHelpers.AssertTrue(true, message);

    public static void Fail(string? message = null) => AssertionHelpers.AssertTrue(false, message ?? "Assertion failed.");
}

internal static partial class Is
{
    public static Constraint True => new PredicateConstraint(static actual => actual is bool value && value, "true");

    public static Constraint False => new PredicateConstraint(static actual => actual is bool value && !value, "false");

    public static Constraint Null => new PredicateConstraint(static actual => actual is null, "null");

    public static Constraint Empty => new PredicateConstraint(AssertionHelpers.IsEmpty, "empty");

    public static NotBuilder Not { get; } = new();

    public static EqualConstraint EqualTo(object? expected) => new(expected);

    public static Constraint GreaterThan(object expected) => new CompareConstraint(expected, static value => value > 0, "greater than");

    public static Constraint GreaterThanOrEqualTo(object expected) => new CompareConstraint(expected, static value => value >= 0, "greater than or equal to");

    public static Constraint LessThan(object expected) => new CompareConstraint(expected, static value => value < 0, "less than");

    public static Constraint LessThanOrEqualTo(object expected) => new CompareConstraint(expected, static value => value <= 0, "less than or equal to");

    public static Constraint InRange(object min, object max) => new RangeConstraint(min, max);

    public static Constraint InstanceOf<T>() => new PredicateConstraint(actual => actual is T, $"instance of {typeof(T).FullName}");

    public static Constraint AssignableTo<T>() => new PredicateConstraint(actual => actual is T, $"assignable to {typeof(T).FullName}");

    public static Constraint TypeOf<T>() => new PredicateConstraint(actual => actual?.GetType() == typeof(T), $"type {typeof(T).FullName}");

    internal sealed class NotBuilder
    {
        public Constraint Null => new PredicateConstraint(static actual => actual is not null, "not null");

        public Constraint Empty => new PredicateConstraint(static actual => !AssertionHelpers.IsEmpty(actual), "not empty");
    }
}

internal static class Does
{
    public static Constraint Contain(object? expected) => new ContainsConstraint(expected);
}

internal static class Has
{
    public static CountBuilder Count { get; } = new();

    public static CountBuilder Length { get; } = new();

    internal sealed class CountBuilder
    {
        public Constraint EqualTo(int expected) => new CountConstraint(expected);
    }
}

internal static class Contains
{
    public static Constraint Key(object? key) => new ContainsKeyConstraint(key);
}

internal static class Throws
{
    public static ThrowsConstraint InstanceOf<TException>()
        where TException : Exception =>
        new(typeof(TException));
}

internal abstract class Constraint
{
    public abstract void Apply(object? actual, string? message);
}

internal sealed class ThrowsConstraint
{
    private readonly Type _exceptionType;

    public ThrowsConstraint(Type exceptionType) => _exceptionType = exceptionType;

    public void Apply(Func<Task> action, string? message)
    {
        try
        {
            action().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AssertionHelpers.AssertTrue(_exceptionType.IsInstanceOfType(ex), message ?? $"Expected exception assignable to {_exceptionType.FullName}, but got {ex.GetType().FullName}.");
            return;
        }

        AssertionHelpers.AssertTrue(false, message ?? $"Expected exception assignable to {_exceptionType.FullName}, but no exception was thrown.");
    }
}

internal sealed class EqualConstraint : Constraint
{
    private readonly object? _expected;
    private TimeSpan? _tolerance;

    public EqualConstraint(object? expected) => _expected = expected;

    public EqualConstraint Within(TimeSpan tolerance)
    {
        _tolerance = tolerance;
        return this;
    }

    public override void Apply(object? actual, string? message)
    {
        var success = _tolerance.HasValue
            ? AssertionHelpers.AreEqualWithin(actual, _expected, _tolerance.Value)
            : AssertionHelpers.AreEqual(actual, _expected);

        AssertionHelpers.AssertTrue(success, message ?? $"Expected {AssertionHelpers.Format(actual)} to equal {AssertionHelpers.Format(_expected)}.");
    }
}

internal sealed class PredicateConstraint : Constraint
{
    private readonly Func<object?, bool> _predicate;
    private readonly string _expectation;

    public PredicateConstraint(Func<object?, bool> predicate, string expectation)
    {
        _predicate = predicate;
        _expectation = expectation;
    }

    public override void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(_predicate(actual), message ?? $"Expected {AssertionHelpers.Format(actual)} to be {_expectation}.");
}

internal sealed class CompareConstraint : Constraint
{
    private readonly object _expected;
    private readonly Func<int, bool> _predicate;
    private readonly string _expectation;

    public CompareConstraint(object expected, Func<int, bool> predicate, string expectation)
    {
        _expected = expected;
        _predicate = predicate;
        _expectation = expectation;
    }

    public override void Apply(object? actual, string? message)
    {
        var comparison = AssertionHelpers.Compare(actual, _expected);
        AssertionHelpers.AssertTrue(_predicate(comparison), message ?? $"Expected {AssertionHelpers.Format(actual)} to be {_expectation} {AssertionHelpers.Format(_expected)}.");
    }
}

internal sealed class RangeConstraint : Constraint
{
    private readonly object _min;
    private readonly object _max;

    public RangeConstraint(object min, object max)
    {
        _min = min;
        _max = max;
    }

    public override void Apply(object? actual, string? message)
    {
        var aboveMin = AssertionHelpers.Compare(actual, _min) >= 0;
        var belowMax = AssertionHelpers.Compare(actual, _max) <= 0;
        AssertionHelpers.AssertTrue(aboveMin && belowMax, message ?? $"Expected {AssertionHelpers.Format(actual)} to be in range {AssertionHelpers.Format(_min)}..{AssertionHelpers.Format(_max)}.");
    }
}

internal sealed class ContainsConstraint : Constraint
{
    private readonly object? _expected;

    public ContainsConstraint(object? expected) => _expected = expected;

    public override void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(AssertionHelpers.Contains(actual, _expected), message ?? $"Expected {AssertionHelpers.Format(actual)} to contain {AssertionHelpers.Format(_expected)}.");
}

internal sealed class CountConstraint : Constraint
{
    private readonly int _expected;

    public CountConstraint(int expected) => _expected = expected;

    public override void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(AssertionHelpers.TryGetCount(actual, out var count) && count == _expected, message ?? $"Expected count {AssertionHelpers.Format(_expected)}, but got {AssertionHelpers.Format(actual)}.");
}

internal sealed class ContainsKeyConstraint : Constraint
{
    private readonly object? _key;

    public ContainsKeyConstraint(object? key) => _key = key;

    public override void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(AssertionHelpers.ContainsKey(actual, _key), message ?? $"Expected dictionary to contain key {AssertionHelpers.Format(_key)}.");
}

internal sealed class EquivalentConstraint : Constraint
{
    private readonly object? _expected;

    public EquivalentConstraint(object? expected) => _expected = expected;

    public override void Apply(object? actual, string? message) =>
        AssertionHelpers.AssertTrue(AssertionHelpers.AreEquivalent(actual, _expected), message ?? $"Expected {AssertionHelpers.Format(actual)} to be equivalent to {AssertionHelpers.Format(_expected)}.");
}

internal static partial class Is
{
    public static Constraint EquivalentTo(object? expected) => new EquivalentConstraint(expected);

    public static Constraint EquivalentTo<T>(IEnumerable<T> expected) => new EquivalentConstraint(expected);

    public static Constraint SameAs(object? expected) => new PredicateConstraint(actual => ReferenceEquals(actual, expected), $"same reference as {AssertionHelpers.Format(expected)}");
}

internal static class AssertionHelpers
{
    public static void AssertTrue(bool condition, string? message) =>
        AssertTrueAsync(condition).GetAwaiter().GetResult();

    private static async Task AssertTrueAsync(bool condition) =>
        await TUnitAssert.That(condition).IsTrue();

    public static bool AreEqual(object? actual, object? expected)
    {
        if (ReferenceEquals(actual, expected))
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            return false;
        }

        if (actual is string || expected is string)
        {
            return string.Equals(actual.ToString(), expected.ToString(), StringComparison.Ordinal);
        }

        if (IsNumeric(actual) && IsNumeric(expected))
        {
            return Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
        }

        if (actual is IEnumerable actualEnumerable && expected is IEnumerable expectedEnumerable)
        {
            return SequenceEqual(actualEnumerable, expectedEnumerable);
        }

        return object.Equals(actual, expected);
    }

    public static bool AreEqualWithin(object? actual, object? expected, TimeSpan tolerance)
    {
        if (actual is DateTime actualDateTime && expected is DateTime expectedDateTime)
        {
            return (actualDateTime - expectedDateTime).Duration() <= tolerance;
        }

        if (actual is DateTimeOffset actualOffset && expected is DateTimeOffset expectedOffset)
        {
            return (actualOffset - expectedOffset).Duration() <= tolerance;
        }

        if (actual is TimeSpan actualTimeSpan && expected is TimeSpan expectedTimeSpan)
        {
            return (actualTimeSpan - expectedTimeSpan).Duration() <= tolerance;
        }

        return AreEqual(actual, expected);
    }

    public static bool AreEquivalent(object? actual, object? expected)
    {
        if (actual is not IEnumerable actualEnumerable || actual is string || expected is not IEnumerable expectedEnumerable || expected is string)
        {
            return AreEqual(actual, expected);
        }

        var remaining = expectedEnumerable.Cast<object?>().ToList();
        foreach (var item in actualEnumerable.Cast<object?>())
        {
            var index = remaining.FindIndex(candidate => AreEqual(item, candidate));
            if (index < 0)
            {
                return false;
            }

            remaining.RemoveAt(index);
        }

        return remaining.Count == 0;
    }

    public static bool Contains(object? actual, object? expected)
    {
        if (actual is string text)
        {
            return expected is not null && text.Contains(expected.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return actual is IEnumerable enumerable && enumerable.Cast<object?>().Any(item => AreEqual(item, expected));
    }

    public static bool ContainsKey(object? actual, object? key)
    {
        if (actual is IDictionary dictionary)
        {
            if (key is null)
            {
                return false;
            }

            return dictionary.Contains(key);
        }

        var containsKey = actual?.GetType().GetMethod("ContainsKey", [key?.GetType() ?? typeof(object)]);
        return containsKey is not null && containsKey.Invoke(actual, [key]) is true;
    }

    public static bool IsEmpty(object? actual)
    {
        if (actual is null)
        {
            return false;
        }

        if (actual is string text)
        {
            return text.Length == 0;
        }

        return TryGetCount(actual, out var count)
            ? count == 0
            : actual is IEnumerable enumerable && !enumerable.Cast<object?>().Any();
    }

    public static bool TryGetCount(object? actual, out int count)
    {
        switch (actual)
        {
            case ICollection collection:
                count = collection.Count;
                return true;
            case null:
                count = 0;
                return false;
        }

        var countProperty = actual.GetType().GetProperty("Count") ?? actual.GetType().GetProperty("Length");
        if (countProperty?.GetValue(actual) is int propertyCount)
        {
            count = propertyCount;
            return true;
        }

        count = 0;
        return false;
    }

    public static int Compare(object? actual, object expected)
    {
        if (actual is null)
        {
            return -1;
        }

        if (actual.GetType() == expected.GetType() && actual is IComparable comparable)
        {
            return comparable.CompareTo(expected);
        }

        var actualDecimal = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
        var expectedDecimal = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
        return actualDecimal.CompareTo(expectedDecimal);
    }

    public static string Format(object? value) => value switch
    {
        null => "<null>",
        string text => "\"" + text + "\"",
        IEnumerable enumerable when value is not string => "[" + string.Join(", ", enumerable.Cast<object?>()) + "]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static bool IsNumeric(object value) => Type.GetTypeCode(value.GetType()) switch
    {
        TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
        _ => false,
    };

    private static bool SequenceEqual(IEnumerable actual, IEnumerable expected)
    {
        using var actualEnumerator = actual.Cast<object?>().GetEnumerator();
        using var expectedEnumerator = expected.Cast<object?>().GetEnumerator();
        while (true)
        {
            var actualHasNext = actualEnumerator.MoveNext();
            var expectedHasNext = expectedEnumerator.MoveNext();
            if (!actualHasNext || !expectedHasNext)
            {
                return actualHasNext == expectedHasNext;
            }

            if (!AreEqual(actualEnumerator.Current, expectedEnumerator.Current))
            {
                return false;
            }
        }
    }
}
