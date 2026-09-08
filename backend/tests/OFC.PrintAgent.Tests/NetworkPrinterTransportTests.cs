using System.Net;
using System.Net.Sockets;
using OFC.PrintAgent;
using Xunit;

namespace OFC.PrintAgent.Tests;

// A real thermal printer speaks raw bytes over TCP port 9100 — this stands a loopback listener in for
// one, so the transport is exercised against actual socket I/O rather than mocked away.
public class NetworkPrinterTransportTests
{
    [Fact]
    public async Task SendAsync_delivers_exact_bytes_to_a_listening_printer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var payload = new byte[] { 0x1B, 0x40, 0x41, 0x42, 0x43, 0x1D, 0x56, 0x00 };
        var transport = new NetworkPrinterTransport("127.0.0.1", port);
        var sendTask = transport.SendAsync(payload, CancellationToken.None);

        using var server = await acceptTask;
        using var stream = server.GetStream();
        var received = new byte[payload.Length];
        var read = 0;
        while (read < received.Length) read += await stream.ReadAsync(received.AsMemory(read));
        await sendTask;

        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task SendAsync_throws_when_nothing_is_listening()
    {
        // Port 1 is reserved and refuses connections almost instantly on every platform.
        var transport = new NetworkPrinterTransport("127.0.0.1", 1);
        await Assert.ThrowsAnyAsync<Exception>(() => transport.SendAsync([0x00], CancellationToken.None));
    }
}
