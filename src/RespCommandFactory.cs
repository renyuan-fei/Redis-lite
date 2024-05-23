using System.Collections.Concurrent;

using codecrafters_redis.Commands;
using codecrafters_redis.Enums;
using codecrafters_redis.Interface;
using codecrafters_redis.Service;

namespace codecrafters_redis;

public class RespCommandFactory
{
  private readonly ExpiredTasks                          _expiredTasks;
  private readonly RespRequest                           _request;
  private readonly ConcurrentDictionary<string, byte[ ]> _simpleStore;
  private readonly RedisServer                           _redisServer;

  public RespCommandFactory(
      byte[ ]                               buffer,
      ConcurrentDictionary<string, byte[ ]> simpleStore,
      ExpiredTasks                          expiredTasks,
      RedisServer                           redisServer)
  {
    _request = new RespRequest(buffer);
    _simpleStore = simpleStore;
    _expiredTasks = expiredTasks;
    _redisServer = redisServer;
  }

  public IRespCommand Create()
  {
    switch (_request.CommandType)
    {
      case RespCommandType.Ping : return new PingCommand();
      case RespCommandType.Echo : return new EchoCommand(_request.Arguments[0]);

      case RespCommandType.Set :
        var setResult =
            new SetCommand(_simpleStore, _request.Arguments[0], _request.Arguments[1]);

        if (_request.Arguments.Count > 2
         && _request.Arguments[2].Equals("px", StringComparison.CurrentCultureIgnoreCase))
        {
          _expiredTasks.AddExpirationTask(_request.Arguments[0],
                                          int.Parse(_request.Arguments[3]));
        }

        return setResult;

      case RespCommandType.Get : return new GetCommand(_simpleStore, _request.Arguments[0], _expiredTasks);

      case RespCommandType.Info : return new InfoCommand(_redisServer);

      case RespCommandType.ReplConf : return new ReplConfCommand();

      case RespCommandType.PSync : return new PSyncCommand(_redisServer);

      default : throw new Exception($"Unexpected command type {_request.CommandType}");
    }
  }
}
