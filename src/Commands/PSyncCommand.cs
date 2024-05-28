using System.Net.Sockets;
using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class PSyncCommand : IRespCommand, IPostExecutionCommand
{
  private readonly RedisServer _redisServer;

  public PSyncCommand(RedisServer redisServer) { _redisServer = redisServer; }

  public RespResponse Execute()
  {
    return new RespResponse(RespDataType.SimpleString, $"FULLRESYNC {_redisServer.MasterReplid} 0");
  }

  public Action<Socket> PostExecutionAction => ExecutionAction;

  async private void ExecutionAction(Socket socket)
  {
    // RDB File in base64 format
    const string base64Rdb = "UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==";

    // Decimal equivalent of the length based on the number of bytes
    string lengthInBytes = base64Rdb.Length.ToString();

    // RDB file format to send
    string payload = $"${lengthInBytes}\r\n{base64Rdb}\r\n";

    byte[] rdbData = Encoding.ASCII.GetBytes(payload);
    await socket.SendAsync(rdbData, SocketFlags.None);
  }
}
