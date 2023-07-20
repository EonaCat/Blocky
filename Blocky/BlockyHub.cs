using Microsoft.AspNetCore.SignalR;
using System.Threading;
using System;
using System.Threading.Tasks;
using EonaCat.Blocky.Models;
using EonaCat.Logger;

namespace EonaCat.Blocky
{
    public class BlockyHub : Hub
    {
        private readonly IHubContext<BlockyHub> _context;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public BlockyHub(IHubContext<BlockyHub> blockyHubContext)
        {
            _context = blockyHubContext;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override Task OnConnectedAsync()
        {
            return GetUpdatedDataAsync(_cancellationTokenSource.Token);
        }

        private Statistics _previousData; // Track the previous data

        public async Task GetUpdatedDataAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var newData = await GetDataAsync().ConfigureAwait(false);
                    if (!DataChanged(newData))
                    {
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        continue; // Skip sending if data hasn't changed
                    }

                    _previousData = newData;
                    await _context.Clients.All.SendAsync("updateData", newData, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Handle exceptions gracefully.
                    Logger.Log($"Error in GetUpdatedDataAsync(): {ex.Message}", ELogType.ERROR);
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }

        private bool DataChanged(Statistics newData)
        {
            return _previousData == null || !newData.Equals(_previousData);
        }

        private static async Task<Statistics> GetDataAsync()
        {
            var dnsStats = await BlockyDns.GetDnsStatsAsync().ConfigureAwait(false);
            return new Statistics
            {
                TotalAllowList = dnsStats.TotalAllowList,
                TotalBlockList = dnsStats.TotalBlockList,
                TotalBlocked = dnsStats.TotalBlocked,
                TotalQueries = dnsStats.TotalQueries,
                TotalNoError = dnsStats.TotalNoError,
                TotalRefused = dnsStats.TotalRefused,
                TotalServerFailure = dnsStats.TotalServerFailure,
                TotalClients = dnsStats.TotalClients,
                TotalCached = dnsStats.TotalCached,
            };
        }
    }
}