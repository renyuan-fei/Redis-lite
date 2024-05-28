using System.Net.Sockets;

namespace codecrafters_redis.Interface;

public interface IRespCommand
{
  RespResponse   Execute();
}
