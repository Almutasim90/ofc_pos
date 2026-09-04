namespace OFC.SharedKernel;

public static class OfflineId
{
    public static Guid New() => Guid.CreateVersion7();
}
