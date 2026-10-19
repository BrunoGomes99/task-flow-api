using System.Globalization;
using System.Text;

namespace TaskFlow.Infrastructure.Messaging;

public static class DueSoonAttempts
{
    public static int Read(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(DueSoonTopology.AttemptHeader, out var raw) || raw is null)
            return 1;

        return raw switch
        {
            byte value => value,
            short value => value,
            int value => value,
            long value => checked((int)value),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 1,
        };
    }
}
