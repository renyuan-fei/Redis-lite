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
    // Binary data for the empty RDB file
    byte[] rdbFile = Convert.FromBase64String("UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==");

    // send the build packet to the client
    // $<length>\r\n<contents>
    byte[] packet = Encoding.UTF8.GetBytes($"${rdbFile.Length}\r\n")  // Length of the RDB file (in bytes)
                            .Concat(rdbFile)           // Content of the RDB file
                            .ToArray();

    await socket.SendAsync(packet, SocketFlags.None);
  }
}
