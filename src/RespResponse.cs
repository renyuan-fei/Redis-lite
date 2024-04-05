using codecrafters_redis.Enums;

namespace codecrafters_redis;

public class RespResponse
{
  private const char   SimpleStringPrefix = '+';
  private const string Suffix             = "\r\n";

  private readonly RespRequest _request;

  public RespResponse(RespRequest request) { _request = request; }

  // get the response
  public string GetResponse()
  {
    switch (_request.CommandType)
    {
      case RespCommandType.Ping :
        return $"{SimpleStringPrefix}PONG{Suffix}";
      case RespCommandType.Echo :
        return $"{SimpleStringPrefix}{_request.Arguments.FirstOrDefault()}{Suffix}";
      default :
        throw new ArgumentOutOfRangeException($"Unexpected Command type {_request.CommandType}");
    }
  }
}
