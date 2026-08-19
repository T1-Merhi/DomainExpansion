/// <summary>
/// Hex colour parsing shared by weapon and enemy definitions.
///
/// Packs to RGBA in a uint so simulation types can carry colour without
/// depending on a rendering colour type.
/// </summary>
public static class ColorHex
{
    public const uint White = 0xFFFFFFFFu;

    public static uint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return White;

        string hex = value.TrimStart('#');
        if (hex.Length == 6) hex += "FF";
        if (hex.Length != 8) return White;

        return uint.TryParse(hex,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out uint parsed)
            ? parsed
            : White;
    }
}
