static class CommandLogFormatter
{
    const string RedactedValue = "***REDACTED***";
    const string ApiKeyOption = "--api-key";

    internal static string Format(string fileName, IReadOnlyCollection<string> arguments)
    {
        var formattedArguments = new List<string>(arguments.Count);
        var redactNextValue = false;

        foreach (var argument in arguments)
        {
            if (redactNextValue)
            {
                formattedArguments.Add(RedactedValue);
                redactNextValue = false;
                continue;
            }

            if (argument.StartsWith($"{ApiKeyOption}=", StringComparison.OrdinalIgnoreCase))
            {
                formattedArguments.Add($"{ApiKeyOption}={RedactedValue}");
                continue;
            }

            formattedArguments.Add(QuoteArgument(argument));
            redactNextValue = string.Equals(argument, ApiKeyOption, StringComparison.OrdinalIgnoreCase);
        }

        return string.Join(" ", new[] { fileName }.Concat(formattedArguments));
    }

    static string QuoteArgument(string argument)
    {
        return argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;
    }
}
