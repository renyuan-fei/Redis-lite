using System.Net.Sockets;

namespace codecrafters_redis.Interface;

public interface IPostExecutionCommand {
  Action<Socket> PostExecutionAction { get; }
}
