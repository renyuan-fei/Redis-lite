using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class PSyncCommand : IRespCommand
{
  private readonly RedisServer _redisServer;

  public PSyncCommand(RedisServer redisServer) { _redisServer = redisServer; }
  public RespResponse Execute()
  {
    return new RespResponse(RespDataType.SimpleString, $"FULLRESYNC {_redisServer.MasterReplid} 0");
  }
}
