using Hangfire;

public static class JobScheduler
{
    public static void ConfigureRecurringJobs()
    {
        // Cleanup old logs daily at 2 AM
        RecurringJob.AddOrUpdate<IBackgroundJobService>(
            "cleanup-old-logs",
            service => service.CleanupOldLogsAsync(),
            Cron.Daily(2),
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Local
            }
        );
    }
}
