using System.Collections.Concurrent;
using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;
using codecrafters_redis.Utils;

namespace codecrafters_redis.Commands;

public class SetCommand : IRespCommand
{
  private readonly string                                _name;
  private readonly string                                _value;
  private readonly ConcurrentDictionary<string, byte[ ]> _workingSet;
  private readonly RedisServer                           _redisServer;

  public SetCommand(
      ConcurrentDictionary<string, byte[ ]> workingSet,
      string                                name,
      string                                value,
      RedisServer                           redisServer)
  {
    _workingSet = workingSet;
    _name = name;
    _value = value;
    _redisServer = redisServer;
  }

  public RespResponse Execute()
  {
    var bytes = Encoding.UTF8.GetBytes(_value);

    // add new key and value
    if (_workingSet.TryAdd(_name, bytes))
    {
      if (_redisServer.Role == RedisRole.Master)
      {
        _redisServer.PropagateCommandToReplicas($"SET {_name} {_value}");
      }

      return new RespResponse(RespDataType.SimpleString, "OK");
    }

    // update existing key
    _workingSet[_name] = bytes;

    if (_redisServer.Role == RedisRole.Master)
    {
      _redisServer.PropagateCommandToReplicas($"SET {_name} {_value}");
    }

    return new RespResponse(RespDataType.SimpleString, "OK");
  }
}
