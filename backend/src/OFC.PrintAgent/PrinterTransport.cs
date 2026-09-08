using System.IO.Ports;
using System.Net.Sockets;

namespace OFC.PrintAgent;

// A PrinterConfiguration.DeviceName follows one of two conventions, matching how thermal POS
// printers are actually wired in the field:
//   "192.168.1.50:9100"  -> network printer, raw ESC/POS over TCP (the near-universal "port 9100" convention)
//   "COM3" / "/dev/ttyUSB0" -> serial/USB-serial printer, raw ESC/POS over a serial port
// A cash drawer is not its own transport: it is wired through a receipt printer's drawer-kick port,
// so it is addressed via the same PrinterConfiguration.DeviceName as that printer.
public interface IPrinterTransport
{
    Task SendAsync(byte[] data, CancellationToken ct);
}

public static class PrinterTransportFactory
{
    public static IPrinterTransport? Create(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return null;
        var value = deviceName.Trim();
        if (TryParseNetwork(value, out var host, out var port)) return new NetworkPrinterTransport(host, port);
        if (LooksLikeSerialPort(value)) return new SerialPrinterTransport(value);
        return null;
    }

    internal static bool TryParseNetwork(string value, out string host, out int port)
    {
        host = ""; port = 0;
        var colon = value.LastIndexOf(':');
        if (colon <= 0 || colon == value.Length - 1) return false;
        if (!int.TryParse(value[(colon + 1)..], out port) || port is <= 0 or > 65535) return false;
        host = value[..colon];
        return host.Length > 0;
    }

    internal static bool LooksLikeSerialPort(string value) =>
        value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || value.StartsWith("/dev/", StringComparison.Ordinal);
}

public sealed class NetworkPrinterTransport(string host, int port) : IPrinterTransport
{
    public async Task SendAsync(byte[] data, CancellationToken ct)
    {
        using var client = new TcpClient();
        client.SendTimeout = 5000;
        var connect = client.ConnectAsync(host, port, ct);
        if (await Task.WhenAny(connect.AsTask(), Task.Delay(5000, ct)) != connect.AsTask())
            throw new TimeoutException($"Timed out connecting to printer at {host}:{port}.");
        await connect;
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }
}

public sealed class SerialPrinterTransport(string portName) : IPrinterTransport
{
    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        using var port = new SerialPort(portName, 9600) { WriteTimeout = 5000 };
        port.Open();
        port.Write(data, 0, data.Length);
        port.BaseStream.Flush();
        return Task.CompletedTask;
    }
}
