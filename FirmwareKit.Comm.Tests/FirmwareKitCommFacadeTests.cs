using FirmwareKit.Comm;
using FirmwareKit.Comm.Usb.Abstractions;
using FirmwareKit.Comm.Usb.Diagnostics;

namespace FirmwareKit.Comm.Tests;

public sealed class FirmwareKitCommFacadeTests
{
    [Fact]
    public void UsbDeviceFilter_Matches_AppliesInterfaceCriteria()
    {
        var info = new UsbDeviceInfo
        {
            VendorId = 0x05C6,
            ProductId = 0x9008,
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0xFF,
            InterfaceProtocol = 0xFF
        };

        var matching = new UsbDeviceFilter
        {
            VendorId = 0x05C6,
            ProductId = 0x9008,
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0xFF,
            InterfaceProtocol = 0xFF
        };

        var nonMatching = new UsbDeviceFilter
        {
            VendorId = 0x05C6,
            ProductId = 0x9008,
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0x42,
            InterfaceProtocol = 0x03
        };

        Assert.True(matching.Matches(info));
        Assert.False(nonMatching.Matches(info));
    }

    [Fact]
    public void FirmwareKitComm_CanOpenSessionsFromRegisteredProvider()
    {
        IFirmwareKitComm comm = new FirmwareKitComm();
        _ = comm.RegisterUsbApi("custom-facade", () => new FacadeProvider());

        using var sessions = comm.OpenUsbDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var read = session.Read(16, 2000);
        var written = session.Write(new byte[] { 1, 2, 3, 4 }, 4, 2000);

        Assert.Equal(16, read.Length);
        Assert.Equal(4, written);
    }

    [Fact]
    public async Task FirmwareKitComm_CustomSession_CanUseAsyncAdapter()
    {
        IFirmwareKitComm comm = new FirmwareKitComm();
        _ = comm.RegisterUsbApi("custom-facade", () => new FacadeProvider());

        using var sessions = comm.OpenUsbDeviceSessions(UsbApiKind.Auto, new UsbDeviceFilter
        {
            VendorId = 0x1F3A,
            ProductId = 0xEFE8
        });

        var session = Assert.Single(sessions.Sessions);
        var asyncSession = session.AsAsync();

        var read = await asyncSession.ReadAsync(8, 1000);
        var written = await asyncSession.WriteAsync(new byte[] { 1, 2, 3 }, 3, 1000);

        Assert.Equal(8, read.Length);
        Assert.Equal(3, written);
    }

    [Fact]
    public void UsbTrace_TransferObserved_CanReceiveStructuredEvent()
    {
        UsbTransferEvent? captured = null;
        Action<UsbTransferEvent> handler = evt => captured = evt;

        try
        {
            UsbTrace.TransferObserved += handler;
            UsbTrace.EmitTransfer(new UsbTransferEvent
            {
                Backend = "test",
                DevicePath = "mock://trace",
                Operation = UsbTransferOperation.Read,
                RequestedBytes = 16,
                TransferredBytes = 8,
                TimeoutMs = 1000,
                RetryCount = 1,
                NativeErrorCode = 110,
                ElapsedMs = 12,
                Outcome = UsbTransferOutcome.ShortTransfer,
                Message = "short packet"
            });
        }
        finally
        {
            UsbTrace.TransferObserved -= handler;
        }

        Assert.NotNull(captured);
        Assert.Equal("test", captured!.Backend);
        Assert.Equal(UsbTransferOutcome.ShortTransfer, captured.Outcome);
    }

    private sealed class FacadeProvider : IUsbApiProvider
    {
        public string ApiName => "custom-facade";

        public UsbApiKind ApiKind => UsbApiKind.Custom;

        public bool IsSupportedOnCurrentPlatform => true;

        public IReadOnlyList<IUsbDeviceSession> EnumerateDeviceSessions(UsbDeviceFilter? filter = null)
        {
            var session = new FacadeSession();
            if (filter == null || filter.Matches(session.DeviceInfo))
            {
                return new[] { (IUsbDeviceSession)session };
            }

            session.Dispose();
            return Array.Empty<IUsbDeviceSession>();
        }
    }

    private sealed class FacadeSession : IUsbDeviceSession
    {
        public UsbDeviceInfo DeviceInfo { get; } = new()
        {
            ApiName = "custom-facade",
            SourceApiKind = UsbApiKind.Custom,
            DevicePath = "mock://facade",
            VendorId = 0x1F3A,
            ProductId = 0xEFE8,
            InterfaceClass = 0xFF,
            InterfaceSubClass = 0xFF,
            InterfaceProtocol = 0xFF
        };

        public byte[] Read(int length) => new byte[length];

        public byte[] Read(int length, int timeoutMs) => new byte[length];

        public int ReadInto(byte[] buffer, int offset, int length) => length;

        public int ReadInto(byte[] buffer, int offset, int length, int timeoutMs) => length;

        public long Write(byte[] data, int length) => length;

        public long Write(byte[] data, int length, int timeoutMs) => length;

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}
