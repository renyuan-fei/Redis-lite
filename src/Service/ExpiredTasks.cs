using System.Collections.Concurrent;

namespace codecrafters_redis.Service;

public class ExpiredTasks
{
    private readonly SortedDictionary<DateTime, List<string>> _expirationQueue = new();
    private readonly ConcurrentDictionary<string, byte[]>     _workingSet;
    private          Timer                                    _timer;

    public ExpiredTasks(ConcurrentDictionary<string, byte[]> workingSet)
    {
        _workingSet = workingSet;
    }

    public void AddExpirationTask(string key, DateTime expiry)
    {

        lock (_expirationQueue)
        {
            if (_expirationQueue.TryGetValue(expiry, out var value))
            {
                value.Add(key);
            }
            else
            {
                _expirationQueue[expiry] = new List<string> { key };
            }

            _timer.Dispose();

            var now = DateTime.UtcNow;
            var nextExpiry = _expirationQueue.First().Key;

            if (nextExpiry > now)
            {
                var delay = nextExpiry - now;
                _timer = new Timer(DeleteExpiredKeys, null, delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                DeleteExpiredKeys(null);
            }
        }
    }

    private void DeleteExpiredKeys(object? state)
    {
        lock (_expirationQueue)
        {
            if (_expirationQueue.Any())
            {
                var now = DateTime.UtcNow;

                var expiredItems = _expirationQueue.Where(kvp => kvp.Key <= now).ToList();
                foreach (var expiredItem in expiredItems)
                {
                    foreach (var key in expiredItem.Value)
                    {
                        _workingSet.TryRemove(key, out _);
                    }
                    _expirationQueue.Remove(expiredItem.Key);
                }
            }

            if (_expirationQueue.Count != 0)
            {
                var nextExpiry = _expirationQueue.First().Key;
                var delay = nextExpiry - DateTime.UtcNow;
                _timer = new Timer(DeleteExpiredKeys, null, delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer = null;
            }
        }
    }
}