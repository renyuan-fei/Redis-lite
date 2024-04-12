using codecrafters_redis.Enums;

namespace codecrafters_redis.Interface;

public interface IRespRequest
{
  RespCommandType CommandType { get; }
  List<string>    Arguments   { get; }
}
