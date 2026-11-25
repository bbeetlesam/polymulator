using System.Text;

namespace Polymulator
{
    public static class LocalizationKeys
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";

            var builder = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (c == '.' || c == '-' || c == ' ')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != '_') builder.Append('_');
                    continue;
                }

                if (char.IsDigit(c) && builder.Length > 0 && char.IsLetter(builder[builder.Length - 1]))
                    builder.Append('_');

                builder.Append(c);
            }

            return builder.Length > 0 ? builder.ToString() : "unknown";
        }

        public static string Level(string sceneName, string suffix)
        {
            return "level." + Normalize(sceneName) + "." + suffix;
        }

        public static string Fragment(string sceneName, string suffix)
        {
            return "fragment." + Normalize(sceneName) + "." + suffix;
        }
    }
}
