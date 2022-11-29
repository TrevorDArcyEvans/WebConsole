namespace WebConsole;

public static class Statics
{
  // stolen from:
  //    Towel.Statics
  //    https://github.com/ZacharyPatten/Towel/blob/main/Sources/Towel/Statics-Extensions.cs
  public static bool IsDefined<TEnum>(this TEnum value) where TEnum : struct, Enum => Enum.IsDefined<TEnum>(value);
}
