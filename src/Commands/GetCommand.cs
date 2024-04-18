using System.Collections.Concurrent;
using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;
using codecrafters_redis.Service;

namespace codecrafters_redis.Commands;

public class GetCommand : IRespCommand
{
  private readonly ConcurrentDictionary<string, byte[ ]> _workingSet;
  private readonly string                                _name;
  private readonly ExpiredTasks                          _expiredTasks;
  public GetCommand(ConcurrentDictionary<string, byte[ ]> workingSet, string name, ExpiredTasks expiredTasks)
  {
    _workingSet = workingSet;
    _name = name;
    _expiredTasks = expiredTasks;
  }

  public RespResponse Execute()
  {
    if (_expiredTasks.IsExpired(_name))
    {
      _expiredTasks.DeleteKey(_name);
      return new RespResponse(RespDataType.Null, string.Empty);
    }

    if (_workingSet.TryGetValue(_name, out byte[ ]? value))
    {
      return new RespResponse(RespDataType.BulkString, Encoding.UTF8.GetString(value));
    }

    return new RespResponse(RespDataType.Null, string.Empty);
  }
}
