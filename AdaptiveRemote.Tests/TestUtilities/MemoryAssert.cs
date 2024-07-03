
using System.Windows.Documents;

namespace AdaptiveRemote.TestUtilities;

internal static class MemoryAssert
{
    internal static void AreEqual(Memory<byte> expected, Memory<byte> actual)
    {
        Span<byte>.Enumerator expectedIter = expected.Span.GetEnumerator();
        Span<byte>.Enumerator actualIter = actual.Span.GetEnumerator();

        int index = 0;
        while (expectedIter.MoveNext())
        {
            if (!actualIter.MoveNext())
            {
                Assert.AreEqual(expected.Length, actual.Length, "Number of bytes");
                Assert.Fail("Same number of bytes were expected, but {0}.MoveNext() returned false", nameof(actualIter));
            }

            Assert.AreEqual(expectedIter.Current.ToString("X2"), actualIter.Current.ToString("X2"), "actual[{0}]", index);

            index++;
        }

        if (actualIter.MoveNext())
        {
            Assert.AreEqual(expected.Length, actual.Length, "Number of bytes");
            Assert.Fail("Same number of bytes were expected, but {0}.MoveNext() returned true", nameof(actualIter));
        }
    }

    internal static void WriteTo(TestContext? testContext, Memory<byte> expected, Memory<byte> actual)
    {
        if (testContext is null)
        {
            return;
        }

        Span<byte>.Enumerator expectedIter = expected.Span.GetEnumerator();
        Span<byte>.Enumerator actualIter = actual.Span.GetEnumerator();

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
