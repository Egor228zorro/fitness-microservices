using System;

namespace PublicWorkout.Infrastructure;

public class StubUserIdentityProvider : IUserIdentityProvider
{
    public Guid GetCurrentUserId()
    {
        // Временная заглушка для тестов. В реальной реализации это будет заменено на получение ID пользователя
        // из заголовков запроса или токена, переданного через API Gateway, как описано в поисковых результатах.
        return Guid.Parse("00000000-0000-0000-0000-000000000001"); // Фиктивный ID пользователя для тестов
    }
}
