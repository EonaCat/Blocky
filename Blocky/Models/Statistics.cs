namespace EonaCat.Blocky.Models;

public class Statistics
{
    public long TotalAllowList { get; set; }
    public long TotalBlockList { get; set; }
    public long TotalBlocked { get; set; }
    public long TotalNoError { get; set; }
    public long TotalQueries { get; set; }
    public long TotalRefused { get; set; }
    public long TotalServerFailure { get; set; }
    public long TotalClients { get; set; }
    public long TotalCached { get; set; }
}