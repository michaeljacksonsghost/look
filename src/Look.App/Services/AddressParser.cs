namespace Look.Services;
public static class AddressParser
{
 public static Uri Parse(string input)
 {
  var value=input.Trim();
  if(string.IsNullOrWhiteSpace(value)) return new Uri("https://www.google.com");
  if(Uri.TryCreate(value,UriKind.Absolute,out var absolute)&&(absolute.Scheme==Uri.UriSchemeHttp||absolute.Scheme==Uri.UriSchemeHttps)) return absolute;
  if(!value.Contains(' ')&&value.Contains('.')&&Uri.TryCreate("https://"+value,UriKind.Absolute,out var host)) return host;
  return new Uri("https://www.google.com/search?q="+Uri.EscapeDataString(value));
 }
}
