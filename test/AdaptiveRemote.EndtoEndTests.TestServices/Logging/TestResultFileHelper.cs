using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Logging;

public static class TestResultFileHelper
{
    public static void AttachResultFileIfExists(string? filePath, TestContext? testContext)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && testContext != null)
        {
            // Retry a few times in case the file is still being written
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    testContext.AddResultFile(filePath);
                    break;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
