using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class EchoCommand : IRespCommand
{
  private readonly string _toEcho;

  public EchoCommand(string toEcho) { _toEcho = toEcho; }

  public RespResponse Execute()
  {
    return new RespResponse(RespDataType.SimpleString, _toEcho);
  }
}
