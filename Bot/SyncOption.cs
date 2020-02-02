namespace Bot
{
    internal enum SyncOption
    {
        Default = 0,
        SkipDownload = 0b01,
        KeepSyncedAt = 0b10,
    }
}