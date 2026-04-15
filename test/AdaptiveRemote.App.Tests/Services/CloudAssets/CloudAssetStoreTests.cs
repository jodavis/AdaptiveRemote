using FluentAssertions;

namespace AdaptiveRemote.Services.CloudAssets;

[TestClass]
public class CloudAssetStoreTests
{
    [TestMethod]
    public void CloudAssetStore_Get_ThrowsOnMissingKey()
    {
        // Arrange
        CloudAssetStore sut = new();

        // Act
        Action act = () => sut.Get<string>("nonexistent");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Asset 'nonexistent' not found in store.*");
    }

    [TestMethod]
    public void CloudAssetStore_Get_ThrowsOnWrongType()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("test", 42);

        // Act
        Action act = () => sut.Get<string>("test");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Asset 'test' is of type 'Int32', but 'String' was requested.");
    }

    [TestMethod]
    public void CloudAssetStore_SetAndGet_RoundTripsCorrectly()
    {
        // Arrange
        CloudAssetStore sut = new();
        string expectedValue = "test value";

        // Act
        sut.Set("key", expectedValue);
        string actualValue = sut.Get<string>("key");

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [TestMethod]
    public void CloudAssetStore_Set_OverwritesExistingValue()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("key", "initial");

        // Act
        sut.Set("key", "updated");
        string actualValue = sut.Get<string>("key");

        // Assert
        actualValue.Should().Be("updated");
    }

    [TestMethod]
    public void CloudAssetStore_Get_SupportsMultipleAssets()
    {
        // Arrange
        CloudAssetStore sut = new();
        sut.Set("asset1", "value1");
        sut.Set("asset2", 123);
        sut.Set("asset3", true);

        // Act & Assert
        sut.Get<string>("asset1").Should().Be("value1");
        sut.Get<int>("asset2").Should().Be(123);
        sut.Get<bool>("asset3").Should().BeTrue();
    }

    [TestMethod]
    public void CloudAssetStore_ThreadSafety_HandlesSimultaneousReadsAndWrites()
    {
        // Arrange
        CloudAssetStore sut = new();
        const int threadCount = 10;
        const int operationsPerThread = 100;
        List<Exception> exceptions = [];
        object exceptionsLock = new();

        // Pre-populate with some assets
        for (int i = 0; i < threadCount; i++)
        {
            sut.Set($"shared-asset-{i}", $"initial-{i}");
        }

        // Act
        Thread[] threads = new Thread[threadCount];
        for (int threadIndex = 0; threadIndex < threadCount; threadIndex++)
        {
            int currentThreadIndex = threadIndex;
            threads[threadIndex] = new Thread(() =>
            {
                try
                {
                    for (int operation = 0; operation < operationsPerThread; operation++)
                    {
                        // Mix of different operations
                        if (operation % 3 == 0)
                        {
                            // Write to shared assets
                            sut.Set($"shared-asset-{operation % threadCount}", $"thread-{currentThreadIndex}-op-{operation}");
                        }
                        else if (operation % 3 == 1)
                        {
                            // Write to thread-specific assets
                            sut.Set($"thread-{currentThreadIndex}-asset-{operation}", $"value-{operation}");
                        }
                        else
                        {
                            // Read from shared assets
                            try
                            {
                                _ = sut.Get<string>($"shared-asset-{operation % threadCount}");
                            }
                            catch (InvalidOperationException)
                            {
                                // Expected if another thread is modifying or type mismatch
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptionsLock)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
            threads[threadIndex].Start();
        }

        // Wait for all threads to complete
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        // Assert
        exceptions.Should().BeEmpty("thread-safe operations should not throw exceptions");

        // Verify that we can still read all thread-specific assets
        for (int threadIndex = 0; threadIndex < threadCount; threadIndex++)
        {
            for (int operation = 1; operation < operationsPerThread; operation += 3)
            {
                string key = $"thread-{threadIndex}-asset-{operation}";
                string expectedValue = $"value-{operation}";
                sut.Get<string>(key).Should().Be(expectedValue, $"asset {key} should have correct value");
            }
        }
    }
}
