using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace AdaptiveRemote.TestUtilities;

public static class TaskAssert
{
    public static TaskAssertions Should(this Task? task)
        => new(task);
    public static TaskAssertions Should(this ValueTask task)
        => new(task.AsTask());
    public static TaskAssertions<TResult> Should<TResult>(this Task<TResult>? task)
        => new(task);
    public static TaskAssertions<TResult> Should<TResult>(this ValueTask<TResult> task)
        => new(task.AsTask());

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
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithExpectation("Expected {context} to have completed with the result {0}, but ", expectedResult, chain =>
                {
                    chain
                        .WithDefaultIdentifier(nameof(Task<TResult>))
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldNotBeCanceled()
                        .TaskShouldNotBeFaulted()
                        .TaskShouldBeCompleted()
                        .Given(task => ((Task<TResult>)task).Result)
                        .ForCondition(CheckEquivalency)
                        .FailWith(
                            "found {context}.Result={0}",
                            result => result);
                });

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
            : base(instance, AssertionChain.GetOrCreate())
        {
        }

        protected abstract AndConstraint<TAssertions> Continuation();

        [CustomAssertion]
        public AndConstraint<TAssertions> BeSuccessful(string because = "", params object[] becauseArgs)
            => BeComplete(because, becauseArgs);

        [CustomAssertion]
        public AndConstraint<TAssertions> BeComplete(string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be complete{reason}, but ", chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldNotBeCanceled()
                        .TaskShouldNotBeFaulted()
                        .TaskShouldBeCompleted();
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> NotBeComplete(string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be incomplete{reason}, but ", chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldNotBeCanceled()
                        .TaskShouldNotBeFaulted()
                        .TaskShouldNotBeCompleted();
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeCompleteWithin(TimeSpan timeout, string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to complete within {0}ms{reason}, but ", timeout.TotalMilliseconds, chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .ForCondition(TaskIsNotNull)
                        .ForCondition(task => task.ContinueWith(_ => { }).Wait(timeout))
                        .FailWith("it did not.");
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> NotBeCompleteWithin(TimeSpan timeout, string because = "", params object[] becauseArgs)
        {
            DateTime startTime = DateTime.Now;

            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be incomplete after {0}ms{reason}, but ", timeout.TotalMilliseconds, chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .ForCondition(task => !task.ContinueWith(_ => { }).Wait(timeout))
                        .FailWith("it completed in {0}ms.", (DateTime.Now - startTime).TotalMilliseconds);
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeCanceled(string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be canceled{reason}, but ", chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldNotBeFaulted()
                        .ForCondition(TaskIsCanceled)
                        .FailWith("{context}.IsCanceled=False");
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeCanceledWithin(TimeSpan timeout, string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be canceled after {0}ms{reason}, but ", timeout.TotalMilliseconds, chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldNotBeFaulted()
                        .ForCondition(task => task.ContinueWith(_ => { }).Wait(timeout))
                        .FailWith("it did not complete.")
                        .Then
                        .ForCondition(TaskIsCanceled)
                        .FailWith("{context}.IsCanceled=False");
                });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeFaultedWith(Exception expectedException, TimeSpan within, string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
               .BecauseOf(because, becauseArgs)
               .WithDefaultIdentifier(nameof(Task))
               .WithExpectation("Expected {context} to be faulted{reason}, but ", chain =>
               {
                   chain
                       .Given(() => Subject)
                       .TaskShouldNotBeNull()
                       .TaskShouldBeCompletedWithin(within)
                       .TaskShouldNotBeCanceled()
                       .TaskShouldBeFaultedWith(expectedException);
               });

            return Continuation();
        }

        [CustomAssertion]
        public AndConstraint<TAssertions> BeFaultedWith(Exception expectedException, string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .WithDefaultIdentifier(nameof(Task))
                .WithExpectation("Expected {context} to be faulted{reason}, but ", chain =>
                {
                    chain
                        .Given(() => Subject)
                        .TaskShouldNotBeNull()
                        .TaskShouldBeCompleted()
                        .TaskShouldNotBeCanceled()
                        .TaskShouldBeFaultedWith(expectedException);
                });

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
            .Then
            .Given(task => task!);
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
    private static GivenSelector<Task> TaskShouldBeCompletedWithin(this GivenSelector<Task> selector, TimeSpan timeout)
        => selector
            .ForCondition(task => task.ContinueWith(_ => { }).Wait(timeout))
            .FailWith("{context}.IsComplete=False after {0}ms.", timeout.TotalMilliseconds)
            .Then;
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeCompleted(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsNotCompleted)
            .FailWith("{context}.IsCompleted=True.")
            .Then;
    [CustomAssertion]
    private static GivenSelector<Task> TaskShouldNotBeCanceled(this GivenSelector<Task> selector)
        => selector
            .ForCondition(TaskIsNotCanceled)
            .FailWith("{context} was canceled.")
            .Then;
    [CustomAssertion]
    private static GivenSelector<Exception> TaskShouldBeFaultedWith(this GivenSelector<Task> selector, Exception expectedException)
        => selector
            .ForCondition(TaskIsFaulted)
            .FailWith("{context}.IsFaulted=False.")
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
                _ => expectedException.Message, exception => exception.Message)
            .Then;
}
