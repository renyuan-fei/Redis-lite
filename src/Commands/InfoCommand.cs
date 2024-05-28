using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class InfoCommand : IRespCommand
{
  private readonly RedisServer _redisServer;

  public InfoCommand(RedisServer redisServer) { _redisServer = redisServer; }

  public RespResponse Execute()
  {
    var info = _redisServer.GetInfo();

    return new RespResponse(RespDataType.BulkString, info);
  }
}
