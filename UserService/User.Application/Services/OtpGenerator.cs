using User.Application.Services.Interfaces;

namespace User.Infrastructure.Services;

public class OtpGenerator : IOtpGenerator
{
    private static readonly Random random = new();

    public string GenerateOtpCode()
    {
        return this.GenerateOtpCode(4); // По умолчанию 4 цифры
    }

    public string GenerateOtpCode(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentException(
                "Length must be greater than 0",
                nameof(length)
            );
        }

        var code = string.Empty;
        for (var i = 0; i < length; i++)
        {
            code += random.Next(0, 10).ToString();
        }

        return code;
    }
}
