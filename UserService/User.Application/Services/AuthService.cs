using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using User.Application.Dtos;
using User.Application.Services.Interfaces;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;
using User.Infrastructure.Settings;

namespace User.Application.Services;

/// <inheritdoc />
public class AuthService(
    IRepositoryProvider repositoryProvider,
    IEmailService emailService,
    IOtpGenerator otpGenerator,
    IOptions<JwtSettings> jwtSettings
) : IAuthService
{
    /// <inheritdoc/>
    public async Task<Result> SendOtpToEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        /* 0. Немедленно реагируем на отмену токена */
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Operation was cancelled",
                cancellationToken
            );
        }

        /* 1. Валидация e-mail */
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            return Result.Fail("Invalid email format");

        try
        {
            /* 2. Поиск пользователя */
            var userRepo = repositoryProvider.GetRepository<UserEntity>();
            var user = await userRepo.GetFirstOrDefaultAsync(
                predicate: u => u.Email == email,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (user is null)
                return Result.Fail("User not found");

            /* 3. Инвалидация всех активных OTP */
            var otpRepo = repositoryProvider.GetRepository<OneTimePassword>();
            var activeOtps = await otpRepo.GetAllAsync(
                predicate: o =>
                    o.UserId == user.Id
                    && !o.IsUsed
                    && o.ExpiresAt > DateTime.UtcNow,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            foreach (var oldOtp in activeOtps)
            {
                oldOtp.IsUsed = true;
                otpRepo.Update(oldOtp);
                repositoryProvider.SaveChanges();
            }

            /* 4. Генерация нового OTP */
            string otpCode;
            try
            {
                otpCode = otpGenerator.GenerateOtpCode();
            }
            catch (Exception ex)
            {
                return Result.Fail($"OTP generation failed: {ex.Message}");
            }

            /* 5. Отправка письма */
            await emailService.SendOtpEmailAsync(email, otpCode, cancellationToken);

            /* 6. Сохранение нового OTP (только после успешной отправки email) */
            var newOtp = new OneTimePassword
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpCode = otpCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
            };

            await otpRepo.InsertAsync(newOtp, cancellationToken);
            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Корректно пробрасываем отмену
            throw;
        }
        catch (Exception ex)
        {
            // Прочие ошибки оборачиваем в Result.Fail
            return Result.Fail($"Error occurred: {ex.Message}");
        }
    }

    public async Task<Result<JwtTokenDto>> GetJwtTokenAsync(
        VerifyOtpRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        /* 0. Немедленно реагируем на отмену токена */
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Operation was cancelled",
                cancellationToken
            );
        }

        /* 1. Валидация входных данных */
        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            return Result.Fail("Invalid email format");

        if (string.IsNullOrWhiteSpace(request.OtpCode))
            return Result.Fail("Invalid OTP code: Code cannot be empty");

        try
        {
            /* 2. Поиск пользователя */
            var userRepo = repositoryProvider.GetRepository<UserEntity>();
            var user = await userRepo.GetFirstOrDefaultAsync(
                predicate: u => u.Email == request.Email,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (user == null)
                return Result.Fail("User not found");

            /* 3. Поиск активного OTP */
            var otpRepo = repositoryProvider.GetRepository<OneTimePassword>();
            var otp = await otpRepo.GetFirstOrDefaultAsync(
                predicate: o =>
                    o.UserId == user.Id
                    && !o.IsUsed
                    && o.ExpiresAt > DateTime.UtcNow,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (otp == null)
                return Result.Fail("OTP not found");

            if (otp.ExpiresAt < DateTime.UtcNow)
                return Result.Fail("OTP expired");

            if (otp.IsUsed)
                return Result.Fail("OTP already used");

            if (otp.OtpCode != request.OtpCode)
                return Result.Fail("OTP code does not match");

            /* 4. Пометка OTP как использованного */
            otp.IsUsed = true;
            otpRepo.Update(otp);
            repositoryProvider.SaveChanges();

            /* 5. Генерация AccessToken и RefreshToken */
            var accessTokenRepo = repositoryProvider.GetRepository<AccessToken>();
            var refreshTokenRepo = repositoryProvider.GetRepository<RefreshToken>();

            var accessToken = new AccessToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = this.GenerateAccessToken(user), // Предполагается метод генерации токена
                ExpiresAt = DateTime.UtcNow.AddHours(1), // Срок действия 1 час
                CreatedAt = DateTime.UtcNow,
            };

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = GenerateRefreshToken(), // Предполагается метод генерации токена
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Срок действия 7 дней
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false,
            };

            await accessTokenRepo.InsertAsync(accessToken, cancellationToken);
            await refreshTokenRepo.InsertAsync(refreshToken, cancellationToken);
            repositoryProvider.SaveChanges();

            /* 6. Формирование результата */
            var jwtTokenDto = new JwtTokenDto(
                AccessToken: accessToken.Token,
                RefreshToken: refreshToken.Token,
                ExpiresAt: accessToken.ExpiresAt
            );

            return Result.Ok(jwtTokenDto);
        }
        catch (OperationCanceledException)
        {
            // Корректно пробрасываем отмену
            throw;
        }
        catch (Exception ex)
        {
            // Прочие ошибки оборачиваем в Result.Fail
            return Result.Fail($"Error occurred: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<JwtTokenDto>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        /* 0. Немедленно реагируем на отмену токена */
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Operation was cancelled",
                cancellationToken
            );
        }

        /* 1. Валидация входных данных */
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Fail("Invalid token: Refresh token cannot be empty");

        try
        {
            /* 2. Поиск RefreshToken */
            var refreshTokenRepo = repositoryProvider.GetRepository<RefreshToken>();
            var tokenEntity = await refreshTokenRepo.GetFirstOrDefaultAsync(
                predicate: t => t.Token == refreshToken,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (tokenEntity == null)
                return Result.Fail("Refresh token not found");

            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                return Result.Fail("Refresh token expired");

            if (tokenEntity.IsRevoked)
                return Result.Fail("Refresh token revoked");

            /* 3. Поиск пользователя */
            var userRepo = repositoryProvider.GetRepository<UserEntity>();
            var user = await userRepo.GetFirstOrDefaultAsync(
                predicate: u => u.Id == tokenEntity.UserId,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (user == null)
                return Result.Fail("User not found");

            /* 4. Генерация нового AccessToken */
            var accessTokenRepo = repositoryProvider.GetRepository<AccessToken>();
            var accessTokenStr = this.GenerateAccessToken(user);

            var accessToken = new AccessToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = accessTokenStr,
                ExpiresAt = DateTime.UtcNow.AddMinutes(
                    jwtSettings.Value.AccessTokenExpirationMinutes
                ),
                CreatedAt = DateTime.UtcNow,
            };

            await accessTokenRepo.InsertAsync(accessToken, cancellationToken);
            repositoryProvider.SaveChanges();

            /* 5. Формирование результата */
            var jwtTokenDto = new JwtTokenDto(
                AccessToken: accessToken.Token,
                RefreshToken: refreshToken, // Возвращаем тот же refresh token
                ExpiresAt: accessToken.ExpiresAt
            );

            return Result.Ok(jwtTokenDto);
        }
        catch (OperationCanceledException)
        {
            // Корректно пробрасываем отмену
            throw;
        }
        catch (Exception ex)
        {
            // Прочие ошибки оборачиваем в Result.Fail
            return Result.Fail($"Error occurred: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        /* 0. Немедленно реагируем на отмену токена */
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Operation was cancelled",
                cancellationToken
            );
        }

        /* 1. Валидация входных данных */
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Fail("Invalid token: Refresh token cannot be empty");

        try
        {
            /* 2. Поиск RefreshToken */
            var refreshTokenRepo = repositoryProvider.GetRepository<RefreshToken>();
            var tokenEntity = await refreshTokenRepo.GetFirstOrDefaultAsync(
                predicate: t => t.Token == refreshToken,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (tokenEntity == null)
                return Result.Fail("Refresh token not found");

            if (tokenEntity.IsRevoked)
                return Result.Fail("Refresh token already revoked");

            /* 3. Отзывание токена */
            tokenEntity.IsRevoked = true;
            refreshTokenRepo.Update(tokenEntity);
            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Корректно пробрасываем отмену
            throw;
        }
        catch (Exception ex)
        {
            // Прочие ошибки оборачиваем в Result.Fail
            return Result.Fail($"Error occurred: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<UserDto>> CreateUserAsync(
        RegisterUserDto registration,
        CancellationToken cancellationToken = default
    )
    {
        /* 0. Немедленно реагируем на отмену токена */
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Operation was cancelled",
                cancellationToken
            );
        }

        /* 1. Валидация входных данных */
        if (
            string.IsNullOrWhiteSpace(registration.Email)
            || !IsValidEmail(registration.Email)
        )
            return Result.Fail("Invalid email format");

        if (string.IsNullOrWhiteSpace(registration.UserName))
            return Result.Fail("Invalid username: Username cannot be empty");

        if (registration.Weight is < 30 or > 300)
            return Result.Fail("Weight must be between 30 and 300 kg");

        if (registration.Height is < 50 or > 250)
            return Result.Fail("Height must be between 50 and 250 cm");

        if (registration.Age is < 13 or > 120)
            return Result.Fail("Age must be between 13 and 120 years");

        if (!Enum.TryParse<Gender>(registration.Gender, out _))
            return Result.Fail("Invalid Gender value. Allowed values: Male, Female");

        if (!Enum.TryParse<ActivityLevel>(registration.ActivityLevel, out _))
        {
            return Result.Fail(
                "Invalid ActivityLevel value. Allowed values: Low, Average, High"
            );
        }

        if (!Enum.TryParse<FitnessGoal>(registration.FitnessGoal, out _))
        {
            return Result.Fail(
                "Invalid FitnessGoal value. Allowed values: WeightLoss, WeightGain, FormMaintence"
            );
        }

        try
        {
            /* 2. Проверка уникальности Email */
            var userRepo = repositoryProvider.GetRepository<UserEntity>();
            var userByEmail = await userRepo.GetFirstOrDefaultAsync(
                predicate: u => u.Email == registration.Email,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (userByEmail != null)
                return Result.Fail("Email already exists");

            /* 3. Проверка уникальности UserName */
            var userByUsername = await userRepo.GetFirstOrDefaultAsync(
                predicate: u => u.UserName == registration.UserName,
                orderBy: null,
                include: null,
                disableTracking: false,
                ignoreQueryFilters: false,
                cancellationToken: cancellationToken
            );

            if (userByUsername != null)
                return Result.Fail("Username already exists");

            /* 4. Создание нового пользователя */
            var newUser = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = registration.Email,
                UserName = registration.UserName,
                Role = Role.Member, // Роль по умолчанию
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
            };

            await userRepo.InsertAsync(newUser, cancellationToken);

            /* 5. Создание профиля пользователя с данными из DTO */
            var userProfileRepo = repositoryProvider.GetRepository<UserProfile>();
            var newProfile = new UserProfile
            {
                UserId = newUser.Id,
                Weight = registration.Weight,
                Height = registration.Height,
                Age = registration.Age,
                Gender = Enum.Parse<Gender>(registration.Gender),
                ActivityLevel = Enum.Parse<ActivityLevel>(
                    registration.ActivityLevel
                ),
                FitnessGoal = Enum.Parse<FitnessGoal>(registration.FitnessGoal),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
            };
            await userProfileRepo.InsertAsync(newProfile, cancellationToken);

            /* 6. Сохранение изменений */
            repositoryProvider.SaveChanges();

            /* 7. Формирование результата */
            var userDto = new UserDto(
                Id: newUser.Id,
                UserName: newUser.UserName,
                Email: newUser.Email,
                Role: newUser.Role.ToString(),
                CreatedAt: newUser.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt: newUser.UpdatedAt
            );

            return Result.Ok(userDto);
        }
        catch (OperationCanceledException)
        {
            // Корректно пробрасываем отмену
            throw;
        }
        catch (Exception ex)
        {
            // Прочие ошибки оборачиваем в Result.Fail
            return Result.Fail($"Error occurred: {ex.Message}");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateRefreshToken()
    {
        // Для refresh token используется случайная строка, так как он не содержит claims
        // и проверяется только по значению в базе данных
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }

        return Convert.ToBase64String(randomNumber);
    }

    private string GenerateAccessToken(UserEntity user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Value.SecretKey)
        );
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Value.Issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                jwtSettings.Value.AccessTokenExpirationMinutes
            ),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
