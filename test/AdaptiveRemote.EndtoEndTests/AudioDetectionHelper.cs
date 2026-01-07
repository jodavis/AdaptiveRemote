using System.Runtime.InteropServices;

namespace AdaptiveRemote.EndtoEndTests;

internal static class AudioDetectionHelper
{
    public static void AssertHasAudioInputAndOutput()
    {
        bool hasOutput = HasDefaultAudioEndpoint(EDataFlow.eRender);
        bool hasInput = HasDefaultAudioEndpoint(EDataFlow.eCapture);

        if (!hasOutput || !hasInput)
        {
            System.Text.StringBuilder messageBuilder = new System.Text.StringBuilder();
            messageBuilder.Append("Console host test requires a default audio ");
            if (!hasOutput && !hasInput)
            {
                messageBuilder.Append("output and input device.");
            }
            else if (!hasOutput)
            {
                messageBuilder.Append("output device.");
            }
            else
            {
                messageBuilder.Append("input device.");
            }

            Assert.Inconclusive(messageBuilder.ToString());
        }
    }

    private static bool HasDefaultAudioEndpoint(EDataFlow flow)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            MMDeviceEnumerator? comEnum = new MMDeviceEnumerator();
            enumerator = comEnum as IMMDeviceEnumerator;
            if (enumerator == null)
            {
                return false;
            }

            int hr = enumerator.GetDefaultAudioEndpoint(flow, ERole.eConsole, out device);
            const int S_OK = 0;
            if (hr != S_OK)
            {
                return false;
            }

            if (device == null)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (device != null)
            {
                try
                {
                    _ = Marshal.ReleaseComObject(device);
                }
                catch
                {
                    // ignore
                }
            }

            if (enumerator != null)
            {
                try
                {
                    _ = Marshal.ReleaseComObject(enumerator);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ClassInterface(ClassInterfaceType.None)]
    private class MMDeviceEnumerator
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        // The full interface has more methods; only minimal signatures are declared here in order.
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

        [PreserveSig]
        int GetDevice(string pwstrId, out IMMDevice ppDevice);

        // Remaining methods omitted because they are not used.
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);

        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);

        [PreserveSig]
        int GetId(out IntPtr ppstrId);

        [PreserveSig]
        int GetState(out int pdwState);
    }
}
