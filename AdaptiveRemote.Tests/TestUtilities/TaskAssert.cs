using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace AdaptiveRemote.TestUtilities;

internal static class TaskAssert
{
    public static TaskAssertions Should(this Task task)
        => new(task);
    public static TaskAssertions Should(this ValueTask task)
        => new(task.AsTask());
    public static TaskAssertions<TResult> Should<TResult>(this Task<TResult> task)
        => new(task);
    public static TaskAssertions<TResult> Should<TResult>(this ValueTask<TResult> task)
        => new(task.AsTask());

    public static void IsComplete(Task? task, string message, params object[] args)
        => IsComplete(task, TimeSpan.Zero, message, args);

    public static void IsComplete(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        WrapTopLevel(() =>
        {
            Assert.IsNotNull(task, message, args);

            WaitForComplete(task, timeout, message, args);

            Assert.IsTrue(task.IsCompleted, "Task.IsCompleted. {0}", FormatMessage(message, args));
            Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
            Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
        });
    }

    public static void IsComplete<T>(ValueTask<T> task, string message, params object[] args)
        => IsComplete(task, TimeSpan.Zero, message, args);

    public static void IsComplete<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsComplete(task.AsTask(), timeout, message, args);

    public static void ResultEquals<T>(Task<T> task, T expected, string message, params object[] args)
        => ResultEquals(task, expected, TimeSpan.Zero, message, args);

    public static void ResultEquals<T>(Task<T> task, T expected, TimeSpan timeout, string message, params object[] args)
    {
        WrapTopLevel(() =>
        {
            IsComplete(task, timeout, message, args);

            Assert.AreEqual(expected, task.Result, "Task.Result. {0}", FormatMessage(message, args));
        });
    }

    public static void ResultEquals<T>(ValueTask<T> task, T expected, string message, params object[] args)
        => ResultEquals(task, expected, TimeSpan.Zero, message, args);

    public static void ResultEquals<T>(ValueTask<T> task, T expected, TimeSpan timeout, string message, params object[] args)
        => ResultEquals(task.AsTask(), expected, timeout, message, args);

    public static void IsNotComplete(Task? task, string message, params object[] args)
        => IsNotComplete(task, TimeSpan.Zero, message, args);

    public static void IsNotComplete(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        WrapTopLevel(() =>
        {
            Assert.IsNotNull(task, message, args);

            WaitForNotComplete(task, timeout, message, args);

            Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
            Assert.IsFalse(task.IsCompleted, "Task.IsCompleted. {0}", FormatMessage(message, args));
        });
    }

    public static void IsNotComplete<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsNotComplete(task.AsTask(), timeout, message, args);

    public static void IsNotComplete<T>(ValueTask<T> task, string message, params object[] args)
        => IsNotComplete(task, TimeSpan.Zero, message, args);

    public static void IsCanceled<T>(ValueTask<T> task, string message, params object[] args)
        => IsCanceled(task, TimeSpan.Zero, message, args);

    public static void IsCanceled<T>(ValueTask<T> task, TimeSpan timeout, string message, params object[] args)
        => IsCanceled(task.AsTask(), timeout, message, args);

    public static void IsCanceled(Task? task, string message, params object[] args)
        => IsCanceled(task, TimeSpan.Zero, message, args);

    public static void IsCanceled(Task? task, TimeSpan timeout, string message, params object[] args)
    {
        WrapTopLevel(() =>
        {
            Assert.IsNotNull(task, message, args);

            WaitForComplete(task, timeout, message, args);

            Assert.IsFalse(task.IsFaulted, "Task.IsFaulted. {0}\n{1}", FormatMessage(message, args), task.Exception?.InnerException);
            Assert.IsTrue(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
        });
    }

    public static void IsFaulted<T>(ValueTask<T> task, Exception expected, string message, params object[] args)
        => IsFaulted(task, expected, TimeSpan.Zero, message, args);

    public static void IsFaulted<T>(ValueTask<T> task, Exception expected, TimeSpan timeout, string message, params object[] args)
        => IsFaulted(task.AsTask(), expected, timeout, message, args);

    public static void IsFaulted(Task? task, Exception expected, string message, params object[] args)
        => IsFaulted(task, expected, TimeSpan.Zero, message, args);

    public static void IsFaulted(Task? task, Exception expected, TimeSpan timeout, string message, params object[] args)
    {
        WrapTopLevel(() =>
        {
            Assert.IsNotNull(task, message, args);

            WaitForComplete(task, timeout, message, args);

            Assert.IsTrue(task.IsCompleted, "Task.IsComplete. {0}", FormatMessage(message, args));

            Assert.IsFalse(task.IsCanceled, "Task.IsCanceled. {0}", FormatMessage(message, args));
            Assert.IsTrue(task.IsFaulted, "Task.IsFaulted. {0}", FormatMessage(message, args));

            Exception actual = task.Exception?.InnerException!;
            Assert.IsNotNull(actual, "Task.Exception. {0}", FormatMessage(message, args));
            Assert.IsInstanceOfType(actual, expected.GetType(), "Task.Exception. {0}", FormatMessage(message, args));
            Assert.AreEqual(expected.Message, actual.Message, "Task.Exception.Message. {0}", FormatMessage(message, args));
        });
    }

    private static void WaitForComplete(Task task, TimeSpan timeout, string message, object[] args)
    {
        if (!task.IsCompleted && timeout > TimeSpan.Zero)
        {
            Assert.IsTrue(task.ContinueWith(_ => { }).Wait(timeout), "Task did not complete within {0}ms. {1}", timeout.TotalMilliseconds, FormatMessage(message, args));
        }
    }

    private static void WaitForNotComplete(Task task, TimeSpan timeout, string message, object[] args)
    {
        if (!task.IsCompleted && timeout > TimeSpan.Zero)
        {
            Assert.IsFalse(task.ContinueWith(_ => { }).Wait(timeout), "Task should not have completed within {0}ms. {1}", timeout.TotalMilliseconds, FormatMessage(message, args));
        }
    }

    private static void WrapTopLevel(Action topLevelAction, [CallerMemberName] string? methodName = default)
    {
        try
        {
            topLevelAction();
        }
        catch (AssertFailedException failedAssert)
        {
            throw new AssertFailedException($"{nameof(TaskAssert)}.{methodName} failed. {failedAssert.Message}");
        }
    }

    private static object FormatMessage(string message, object[] args)
        => args.Length == 0 ? message : new FormattedMessage(message, args);

    private class FormattedMessage
    {
        private string _message;
        private object[] _args;

        public FormattedMessage(string message, object[] args)
        {
            _message = message;
            _args = args;
        }

        public override string ToString() => string.Format(_message, _args);
    }

    public class TaskAssertions : TaskAssertionsBase<TaskAssertions>
    {
        public TaskAssertions(Task? instance)
            : base(instance)
        { }

        protected override string Identifier => nameof(Task);

        protected override AndConstraint<TaskAssertions> Continuation() => new(this);
    }

    public class TaskAssertions<TResult> : TaskAssertionsBase<TaskAssertions<TResult>>
    {
        public TaskAssertions(Task<TResult>? instance)
            : base(instance)
        { }

        protected override string Identifier => nameof(Task<TResult>);

        protected override AndConstraint<TaskAssertions<TResult>> Continuation() => new(this);

        [CustomAssertion]
        public AndConstraint<TaskAssertions<TResult>> HaveResult(TResult expectedResult, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithExpectation("Expected {context} to have completed with the result {0}, but ", expectedResult)
                .WithDefaultIdentifier(nameof(Task<TResult>))
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .TaskShouldNotBeCanceled()
                .TaskShouldNotBeFaulted()
                .TaskShouldBeCompleted()
                .Given(task => ((Task<TResult>)task).Result)
                .ForCondition(CheckEquivalency)
                //.ForCondition(result => (result is null) == (expectedResult is null))
                //.FailWith("found {context}.Result=<null>")
                //.Then
                //.ForCondition(result => result!.Equals(expectedResult))
                .FailWith("found {context}.Result={0}", result => result);

            return Continuation();

            bool CheckEquivalency(TResult actualResult)
            {
                try
                {
                    actualResult.Should().BeEquivalentTo(expectedResult);
                    return true;
                }
                catch (AssertFailedException)
                {
                    return false;
                }
            }
        }
    }

    public abstract class TaskAssertionsBase<TAssertions> : ReferenceTypeAssertions<Task?, TAssertions>
        where TAssertions : ReferenceTypeAssertions<Task?, TAssertions>
    {

        public TaskAssertionsBase(Task? instance)
            : base(instance)
        { }

        protected abstract AndConstraint<TAssertions> Continuation();

        [CustomAssertion]
        public AndConstraint<TAssertions> BeComplete(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} should be complete{reason}, but ")
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .TaskShouldNotBeCanceled()
                .TaskShouldNotBeFaulted()
                .TaskShouldBeCompleted();

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> NotBeComplete(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} should be complete{reason}, but ")
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .TaskShouldNotBeCanceled()
                .TaskShouldNotBeFaulted()
                .TaskShouldNotBeCompleted();

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeCompleteWithin(TimeSpan timeout, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to complete within {0}ms{reason}, but ", timeout.TotalMilliseconds)
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .ForCondition(TaskIsNotNull)
                .ForCondition(task => task.ContinueWith(_ => { }).Wait(timeout))
                .FailWith("it did not.");

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> NotBeCompleteWithin(TimeSpan timeout, string because = "", params object[] becauseArgs)
        {
            DateTime startTime = DateTime.Now;

            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be incomplete after {0}ms{reason}, but ", timeout.TotalMilliseconds)
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .ForCondition(task => !task.ContinueWith(_ => { }).Wait(timeout))
                .FailWith("it completed in {0}ms.", (DateTime.Now - startTime).TotalMilliseconds);

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeCanceled(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be canceled{reason}, but ")
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .TaskShouldNotBeFaulted()
                .ForCondition(TaskIsCanceled)
                .FailWith("{context}.IsCanceled=False");

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeFaultedWith(Exception expectedException, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be faulted{reason}, but ")
                .Given(() => Subject)
                .TaskShouldNotBeNull()
                .TaskShouldBeCompleted()
                .TaskShouldNotBeCanceled()
                .ForCondition(TaskIsFaulted)
                .FailWith("{context}.IsFaulted=False.")
                .Then
                .ClearExpectation()
                .Then
                .Given(task => task.Exception?.InnerException)
                .ForCondition(exception => exception is not null)
                .FailWith("Expected {context}.InnerException.Exception should not be null")
                .Then
                .Given(exception => exception!)
                .ForCondition(exception => exception.GetType() == expectedException.GetType())
                .FailWith("Expected {context}.InnerException.Exception is of type {0}, but found {1}",
                    _ => expectedException.GetType().FullName, exception => exception.GetType().FullName)
                .Then
                .ForCondition(exception => exception.Message == expectedException.Message)
                .FailWith("Expected {context}.InnerException.Exception.Message is {0}, but found {1}",
                    _ => expectedException.Message, exception => exception.Message);

            return Continuation();
        }
    }

    private static bool TaskIsNotNull(Task? task) => task is not null;
    private static bool TaskIsCompleted(Task task) => task.IsCompleted == true;
    private static bool TaskIsNotCompleted(Task task) => task.IsCompleted == false;
    private static bool TaskIsCanceled(Task task) => task.IsCanceled == true;
    private static bool TaskIsNotCanceled(Task task) => task.IsCanceled == false;
    private static bool TaskIsFaulted(Task task) => task.IsFaulted == true;
    private static bool TaskIsNotFaulted(Task task) => task.IsFaulted == false;

    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeNull(this GivenSelector<Task?> selector)
        => selector
            .ForCondition(TaskIsNotNull)
            .FailWith("{context} was <null>.")
            .Then.Given(task => task!);
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeFaulted(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsNotFaulted)
            .FailWith("{context} was faulted with {0}",
                task => task.Exception?.InnerException)
            .Then;
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldBeCompleted(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsCompleted)
            .FailWith("{context}.IsCompleted=False.")
            .Then;
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeCompleted(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsNotCompleted)
            .FailWith("{context}.IsCompleted=False.")
            .Then;
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeCanceled(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsNotCanceled)
            .FailWith("{context} was canceled.")
            .Then;
}
