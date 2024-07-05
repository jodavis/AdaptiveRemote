
using System.Windows.Documents;

namespace AdaptiveRemote.TestUtilities;

internal static class MemoryAssert
{
    internal static void AreEqual(ReadOnlyMemory<byte> expected, ReadOnlyMemory<byte> actual, string name)
    {
        ReadOnlySpan<byte>.Enumerator expectedIter = expected.Span.GetEnumerator();
        ReadOnlySpan<byte>.Enumerator actualIter = actual.Span.GetEnumerator();

        int index = 0;
        while (expectedIter.MoveNext())
        {
            if (!actualIter.MoveNext())
            {
                Assert.AreEqual(expected.Length, actual.Length, "Number of bytes in {0}", name);
                Assert.Fail("Same number of bytes were expected, but {0}.MoveNext() returned false", nameof(actualIter));
            }

            Assert.AreEqual(expectedIter.Current.ToString("X2"), actualIter.Current.ToString("X2"), "{1}}[{0}]", index, name);

            index++;
        }

        if (actualIter.MoveNext())
        {
            Assert.AreEqual(expected.Length, actual.Length, "Number of bytes in {0}", name);
            Assert.Fail("Same number of bytes were expected, but {0}.MoveNext() returned true", nameof(actualIter));
        }
    }

    internal static void WriteTo(TestContext? testContext, ReadOnlyMemory<byte> expected, ReadOnlyMemory<byte> actual)
        => WriteTo(testContext, string.Empty, expected, actual);

    internal static void WriteTo(TestContext? testContext, string description, ReadOnlyMemory<byte> expected, ReadOnlyMemory<byte> actual)
    {
        if (testContext is null)
        {
            return;
        }

        ReadOnlySpan<byte>.Enumerator expectedIter = expected.Span.GetEnumerator();
        ReadOnlySpan<byte>.Enumerator actualIter = actual.Span.GetEnumerator();

        if (description.Length > 0)
        {
            testContext.WriteLine("");
            testContext.WriteLine(description);
        }
        testContext.WriteLine("Exp\tActual");

        while (expectedIter.MoveNext())
        {
            if (actualIter.MoveNext())
            {
                testContext.WriteLine("{0:X2}\t{1:X2}", expectedIter.Current, actualIter.Current);
            }
            else
            {
                do
                {
                    testContext.WriteLine("{0:X2}", expectedIter.Current);
                }
                while (expectedIter.MoveNext());

                break;
            }
        }

        while (actualIter.MoveNext())
        {
            testContext.WriteLine("  \t{0:X2}", actualIter.Current);
        }
    }
}
