using FluentAssertions.Equivalency;

namespace User.Tests;

public class EnumToStringStep : IEquivalencyStep
{
    public EquivalencyResult Handle(
        Comparands comparands,
        IEquivalencyValidationContext context,
        IEquivalencyValidator nestedValidator
    )
    {
        if (
            comparands.Subject is not string subject
            || comparands.Expectation?.GetType().IsEnum != true
        )
            return EquivalencyResult.ContinueWithNext;

        var expectedEnumName = comparands.Expectation.ToString();
        return subject == expectedEnumName
            ? EquivalencyResult.AssertionCompleted
            : EquivalencyResult.ContinueWithNext;
    }
}
