using OFC.PrintAgent;
using Xunit;

namespace OFC.PrintAgent.Tests;

public class PrinterTransportFactoryTests
{
    [Theory]
    [InlineData("192.168.1.50:9100", true, "192.168.1.50", 9100)]
    [InlineData("printer.local:9100", true, "printer.local", 9100)]
    [InlineData("COM3", false, "", 0)]
    [InlineData("not-a-network-address", false, "", 0)]
    [InlineData("bad:port", false, "", 0)]
    public void TryParseNetwork_matches_host_port_convention(string value, bool expected, string host, int port)
    {
        var ok = PrinterTransportFactory.TryParseNetwork(value, out var parsedHost, out var parsedPort);
        Assert.Equal(expected, ok);
        if (expected) { Assert.Equal(host, parsedHost); Assert.Equal(port, parsedPort); }
    }

    [Theory]
    [InlineData("COM3", true)]
    [InlineData("com7", true)]
    [InlineData("/dev/ttyUSB0", true)]
    [InlineData("192.168.1.50:9100", false)]
    public void LooksLikeSerialPort_matches_serial_convention(string value, bool expected) =>
        Assert.Equal(expected, PrinterTransportFactory.LooksLikeSerialPort(value));

    [Fact]
    public void Create_returns_network_transport_for_host_port() =>
        Assert.IsType<NetworkPrinterTransport>(PrinterTransportFactory.Create("10.0.0.5:9100"));

    [Fact]
    public void Create_returns_serial_transport_for_com_port() =>
        Assert.IsType<SerialPrinterTransport>(PrinterTransportFactory.Create("COM4"));

    [Fact]
    public void Create_returns_null_for_unrecognized_device_name() =>
        Assert.Null(PrinterTransportFactory.Create("SharedWindowsPrinter"));

    [Fact]
    public void Create_returns_null_for_missing_device_name() =>
        Assert.Null(PrinterTransportFactory.Create(null));
}
