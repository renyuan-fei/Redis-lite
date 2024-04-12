using codecrafters_redis.Commands;
using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis;

public class RespCommandFactory
{
  private readonly RespRequest                 _request;
  private readonly Dictionary<string, byte[ ]> _simpleStore;

  public RespCommandFactory(byte[ ] buffer, Dictionary<string, byte[ ]> simpleStore)
  {
    _request = new RespRequest(buffer);
    _simpleStore = simpleStore;
  }

  public IRespCommand Create()
  {
    return _request.CommandType switch
    {
        RespCommandType.Ping => new PingCommand(),
        RespCommandType.Echo => new EchoCommand(_request.Arguments[0]),
        RespCommandType.Set => new SetCommand(_simpleStore,
                                              _request.Arguments[0],
                                              _request.Arguments[1]),
        RespCommandType.Get => new GetCommand(_simpleStore, _request.Arguments[0]),
        _ => throw new Exception($"Unexpected command type {_request.CommandType}")
    };
  }
}
