namespace codecrafters_redis.Utils;

public static class RandomStringGenerator
{
  public static string GenerateRandomString(int length)
  {
    var random = new Random();
    const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    return new string(Enumerable.Repeat(chars, length)
                                .Select(s => s[random.Next(s.Length)])
                                .ToArray());
  }
}
