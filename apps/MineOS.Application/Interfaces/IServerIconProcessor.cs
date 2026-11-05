namespace MineOS.Application.Interfaces;

public interface IServerIconProcessor
{
    Task<byte[]> CreatePngAsync(Stream input, CancellationToken ct);
}
