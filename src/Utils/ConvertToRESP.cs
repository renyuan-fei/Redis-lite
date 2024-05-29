using System.Text;

namespace codecrafters_redis.Utils;

public static class  RedisCommandConverter
{
  public static string ToRespFormat(string command)
  {
    string[] words = command.Split(' ');
    StringBuilder sb = new StringBuilder();
    sb.Append('*').Append(words.Length).Append("\r\n");

    foreach (string word in words)
    {
      sb.Append('$').Append(word.Length).Append("\r\n");
      sb.Append(word).Append("\r\n");
    }

    return sb.ToString();
  }
}
