using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class ReplConfCommand : IRespCommand
{
  public RespResponse Execute()
  {
    return new RespResponse(RespDataType.SimpleString, "OK");
  }
}
