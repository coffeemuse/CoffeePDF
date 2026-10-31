namespace CoffeePdf.Core.Helper;

/// <summary>
/// The Helper can't be reached: no <c>channel.json</c>, its process gone, no connection, or no answer in time. Open and
/// Save then use the Mac's own boxes, and a copy's saves stay on the Mac until it's back (spec §6.9).
/// </summary>
public sealed class HelperUnreachableException(string message) : Exception(message);

/// <summary>
/// The Helper answered with an error, <c>{"error": code}</c> and a status other than 200, or with something that isn't
/// a JSON object. Its message is CoffeePDF's own, with the status and code: the channel's errors carry no message.
/// </summary>
public sealed class HelperErrorException(int status, string code) : Exception($"Healdan Helper answered {status} ({code})")
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}
