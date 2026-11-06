using System.Text;
using System.Text.RegularExpressions;

namespace Kalora.Application.Common;

/// <summary>
/// Keeps personal data and secrets out of log files. Logs say what happened and to which ID, never to whom: e-mail addresses
/// and the values of database keys (a unique-constraint error names the duplicate value) are masked before anything is written.
/// </summary>
public static partial class PersonalData
{
    public const string AddressMask = "<address>";
    public const string ValueMask = "<value>";

    [GeneratedRegex(@"[A-Za-z0-9._%+'\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex AddressPattern();

    /// <summary>"Key (email)=(a@b.c) already exists" from the database.</summary>
    [GeneratedRegex(@"\)=\([^)]*\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex KeyValuePattern();

    public static string MaskEmails(string? text) => string.IsNullOrEmpty(text) ? string.Empty : AddressPattern().Replace(text, AddressMask);

    /// <summary>Masks addresses and database key values in free text such as exception messages.</summary>
    public static string MaskForLog(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : KeyValuePattern().Replace(AddressPattern().Replace(text, AddressMask), ")=(" + ValueMask + ")");

    /// <summary>
    /// A stand-in for <paramref name="exception"/> that logs the same (type, message, stack, inner exceptions) with the personal data
    /// masked. Pass it to the logger instead of the original.
    /// </summary>
    public static Exception ForLog(Exception exception) => new MaskedException(exception);

    private sealed class MaskedException(Exception original) : Exception(MaskForLog(original.Message))
    {
        public override string ToString()
        {
            var text = new StringBuilder();
            for (Exception? current = original; current is not null; current = current.InnerException)
            {
                if (current != original)
                {
                    text.Append(" ---> ");
                }

                text.Append(current.GetType().FullName).Append(": ").Append(MaskForLog(current.Message));
                if (current.StackTrace is { } stack)
                {
                    text.AppendLine().Append(MaskForLog(stack));
                }

                text.AppendLine();
            }

            return text.ToString();
        }
    }
}
