using System.Text;
using UnityEngine;

public static class TextFormatter
{
  public static int GetSignificantDecimals(double value, int minDecimals = 0)
  {
    string raw = value.ToString("F3");
    int dotIndex = raw.IndexOf('.');
    int places = 0;
    if (dotIndex >= 0)
    {
      string decimals = raw.Substring(dotIndex + 1).TrimEnd('0');
      places = decimals.Length;
    }
    return Mathf.Max(minDecimals, places);
  }

  public static string FormatMoney(double value)
  {
    int places = GetSignificantDecimals(value, 2);
    return value.ToString($"F{places}");
  }

  public static string FormatSprite(double value, int decimalPlaces)
  {
    string formatted = value.ToString($"F{decimalPlaces}");
    var sb = new StringBuilder();
    foreach (char c in formatted)
    {
      if (c >= '0' && c <= '9') sb.Append($"<sprite={c - '0'}>");
      else if (c == '.') sb.Append("<sprite=10>");
      else if (c == ',') sb.Append("<sprite=11>");
    }
    return sb.ToString();
  }
}
