using System.Collections.Concurrent;

namespace codecrafters_redis.Service;

public class ExpiredTasks
{
  private readonly SortedDictionary<DateTime, List<string>> _expirationQueue = new();
  private readonly ConcurrentDictionary<string, byte[ ]>    _workingSet;
  private          Timer                                    _timer;

  public ExpiredTasks(ConcurrentDictionary<string, byte[ ]> workingSet)
  {
    _workingSet = workingSet;
    _timer = StartExpirationTask();
  }

  public void AddExpirationTask(string key, int expiry)
  {
    lock (_expirationQueue)
    {
      var expiryDate = DateTime.UtcNow.AddMilliseconds(expiry);

      if (_expirationQueue.TryGetValue(expiryDate, out var value)) { value.Add(key); }
      else { _expirationQueue[expiryDate] = [key]; }
    }
  }

  private Timer StartExpirationTask()
  {
    return new Timer(_ => DeleteExpiredKeys(),
                     null,
                     TimeSpan.FromMilliseconds(50),
                     TimeSpan.FromMilliseconds(50));
  }

  public void DeleteKey(string key) { _workingSet.TryRemove(key, out _); }

  private void DeleteExpiredKeys()
  {
    lock (_expirationQueue)
    {
      if (_expirationQueue.Count == 0) return;

      var now = DateTime.UtcNow;

      var expiredItems = _expirationQueue.Where(kvp => kvp.Key <= now).ToList();

      foreach (var expiredItem in expiredItems)
      {
        foreach (var key in expiredItem.Value) { DeleteKey(key); }

        _expirationQueue.Remove(expiredItem.Key);
      }

      if (_expirationQueue.Count <= 0) return;

      {
        var keysToCheck = _expirationQueue.Values.SelectMany(x => x).Take(20).ToList();

        var expiredKeys = keysToCheck
                          .Where(key => _expirationQueue.Any(kvp => kvp.Key <= now
                                  && kvp.Value.Contains(key)))
                          .ToList();

        foreach (var expiredKey in expiredKeys) { DeleteKey(expiredKey); }

        if ((double)expiredKeys.Count / keysToCheck.Count > 0.25) { DeleteExpiredKeys(); }
      }
    }
  }

  public bool IsExpired(string key)
  {
    lock (_expirationQueue)
    {
      var now = DateTime.UtcNow;

      return _expirationQueue.Any(kvp => kvp.Key <= now && kvp.Value.Contains(key));
    }
  }
}
