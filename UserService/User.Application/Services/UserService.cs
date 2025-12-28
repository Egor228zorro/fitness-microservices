using AutoMapper;
using FluentResults;
using User.Application.Dtos;
using User.Application.Services.Interfaces;
using User.Domain.Entity;
using User.Domain.Enum;
using User.Infrastructure;

namespace User.Application.Services;

/// <inheritdoc />
public class UserService(
    IRepositoryProvider repositoryProvider,
    IUserContext userContext,
    IMapper mapper
) : IUserService
{
    /// <inheritdoc/>
    public async Task<Result<UserProfileDto>> GetCurrentUserProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!userContext.IsAuthenticated || userContext.UserId == Guid.Empty)
        {
            return Result.Fail("Unauthorized access");
        }

        var userId = userContext.UserId;
        var userProfileRepository = repositoryProvider.GetRepository<UserProfile>();

        var userProfile = await userProfileRepository.GetFirstOrDefaultAsync(
            predicate: profile => profile.UserId == userId,
            orderBy: null,
            include: null,
            disableTracking: false,
            ignoreQueryFilters: false,
            cancellationToken: cancellationToken
        );

        if (userProfile == null || userProfile.UserId == Guid.Empty)
        {
            // Создаем профиль с дефолтными значениями
            userProfile = new UserProfile
            {
                UserId = (Guid)userId!,
                Weight = 0,
                Height = 0,
                Age = 0,
                Gender = Gender.Male,
                ActivityLevel = ActivityLevel.Low,
                FitnessGoal = FitnessGoal.WeightLoss,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
            };
            await userProfileRepository.InsertAsync(userProfile, cancellationToken);
            repositoryProvider.SaveChanges();
        }

        var userProfileDto = mapper.Map<UserProfileDto>(userProfile);
        return Result.Ok(userProfileDto);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateUserProfileAsync(
        UpdateUserProfileDto? updateDto,
        CancellationToken cancellationToken = default
    )
    {
        // 1. Проверка авторизации
        if (!userContext.IsAuthenticated)
        {
            return Result.Fail("Unauthorized access");
        }

        // 2. Проверка на null DTO
        if (updateDto == null)
        {
            return Result.Fail("DTO cannot be null");
        }

        // 3. Валидация данных
        var validationResult = ValidateUpdateDto(updateDto);
        if (validationResult.IsFailed)
        {
            return validationResult;
        }

        try
        {
            var repository = repositoryProvider.GetRepository<UserProfile>();

            // 4. Поиск существующего профиля
            var existingProfile = await repository.GetFirstOrDefaultAsync(
                x => x.UserId == userContext.UserId,
                null,
                null,
                false,
                false,
                cancellationToken
            );

            if (existingProfile == null)
            {
                return Result.Fail("User profile not found");
            }

            // 5. Обновление полей
            UpdateProfileFields(existingProfile, updateDto);

            // 6. Обновление даты изменения
            existingProfile.UpdatedAt = DateTime.UtcNow;

            // 7. Сохранение изменений
            repository.Update(existingProfile);
            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw; // re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            return Result.Fail($"Database error occurred: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<UserSettingsDto>> GetUserSettingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        // 1. Проверка авторизации
        if (!userContext.IsAuthenticated)
        {
            return Result.Fail<UserSettingsDto>("Unauthorized access");
        }

        // 2. Проверка валидности UserId
        if (userContext.UserId == Guid.Empty)
        {
            return Result.Fail<UserSettingsDto>("Invalid user id");
        }

        try
        {
            var repository = repositoryProvider.GetRepository<UserSettings>();

            // 3. Поиск настроек пользователя
            var userSettings = await repository.GetFirstOrDefaultAsync(
                x => x.UserId == userContext.UserId,
                null,
                null,
                false,
                false,
                cancellationToken
            );

            // 4. Если настройки не найдены, создаем и сохраняем дефолтные настройки
            if (userSettings == null)
            {
                var defaultSettings = new UserSettings
                {
                    UserId = userContext.UserId,
                    Theme = Theme.Light,
                    Language = Language.English,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null,
                };

                await repository.InsertAsync(defaultSettings, cancellationToken);
                repositoryProvider.SaveChanges();

                var defaultDto = mapper.Map<UserSettingsDto>(defaultSettings);
                return Result.Ok(defaultDto);
            }

            // 5. Маппинг найденных настроек в DTO
            var dto = mapper.Map<UserSettingsDto>(userSettings);
            return Result.Ok(dto);
        }
        catch (OperationCanceledException)
        {
            // 6. OperationCanceledException пробрасываем дальше
            throw;
        }
        catch (Exception ex)
        {
            // 7. Остальные исключения преобразуем в Result.Fail
            return Result.Fail<UserSettingsDto>(
                $"Database error occurred: {ex.Message}"
            );
        }
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateUserSettingsAsync(
        UpdateUserSettingsDto updateDto,
        CancellationToken cancellationToken = default
    )
    {
        // 1. Проверка авторизации
        if (!userContext.IsAuthenticated)
        {
            return Result.Fail("Unauthorized access");
        }

        // 2. Проверка валидности UserId
        if (userContext.UserId == Guid.Empty)
        {
            return Result.Fail("Invalid user id");
        }

        // 3. Если все поля null - ничего не делаем, возвращаем успех
        if (
            string.IsNullOrEmpty(updateDto.Theme)
            && string.IsNullOrEmpty(updateDto.Language)
        )
        {
            return Result.Ok();
        }

        // 4. Валидация enum значений
        var validationResult = ValidateEnumValues(updateDto);
        if (validationResult.IsFailed)
        {
            return validationResult;
        }

        try
        {
            var repository = repositoryProvider.GetRepository<UserSettings>();

            // 6. Поиск существующих настроек
            var existingSettings = await repository.GetFirstOrDefaultAsync(
                x => x.UserId == userContext.UserId,
                null,
                null,
                false,
                false,
                cancellationToken
            );

            if (existingSettings == null)
            {
                // 7. Создание новых настроек
                var newSettings = new UserSettings
                {
                    UserId = userContext.UserId,
                    Theme = ParseTheme(updateDto.Theme),
                    Language = ParseLanguage(updateDto.Language),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                await repository.InsertAsync(newSettings, cancellationToken);
            }
            else
            {
                // 8. Обновление существующих настроек
                UpdateSettingsFields(existingSettings, updateDto);
                existingSettings.UpdatedAt = DateTime.UtcNow;

                repository.Update(existingSettings);
            }

            repositoryProvider.SaveChanges();

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // 9. OperationCanceledException пробрасываем дальше
            throw;
        }
        catch (Exception ex)
        {
            // 10. Остальные исключения преобразуем в Result.Fail
            return Result.Fail($"Database error occurred: {ex.Message}");
        }
    }

    private static Result ValidateEnumValues(UpdateUserSettingsDto updateDto)
    {
        // Валидация Theme
        if (!string.IsNullOrEmpty(updateDto.Theme))
        {
            if (!Enum.TryParse<Theme>(updateDto.Theme, true, out _))
            {
                return Result.Fail("Invalid Theme value");
            }
        }

        // Валидация Language
        if (string.IsNullOrEmpty(updateDto.Language))
            return Result.Ok();

        return !Enum.TryParse<Language>(updateDto.Language, true, out _)
            ? Result.Fail("Invalid Language value")
            : Result.Ok();
    }

    private static Theme ParseTheme(string? theme)
    {
        return string.IsNullOrEmpty(theme)
            ? Theme.Light
            : // значение по умолчанию
            Enum.Parse<Theme>(theme, true);
    }

    private static Language ParseLanguage(string? language)
    {
        return string.IsNullOrEmpty(language)
            ? Language.English
            : // значение по умолчанию
            Enum.Parse<Language>(language, true);
    }

    private static void UpdateSettingsFields(
        UserSettings settings,
        UpdateUserSettingsDto updateDto
    )
    {
        // Обновляем только предоставленные поля
        if (!string.IsNullOrEmpty(updateDto.Theme))
        {
            settings.Theme = Enum.Parse<Theme>(updateDto.Theme, true);
        }

        if (!string.IsNullOrEmpty(updateDto.Language))
        {
            settings.Language = Enum.Parse<Language>(updateDto.Language, true);
        }
    }

    private static Result ValidateUpdateDto(UpdateUserProfileDto? updateDto)
    {
        // Валидация веса
        if (updateDto!.Weight is < 0)
        {
            return Result.Fail("Weight cannot be negative");
        }

        // Валидация роста
        if (updateDto.Height is < 0)
        {
            return Result.Fail("Height cannot be negative");
        }

        // Валидация возраста
        if (updateDto.Age is < 0)
        {
            return Result.Fail("Age cannot be negative");
        }

        // Валидация Gender enum
        if (!string.IsNullOrEmpty(updateDto.Gender))
        {
            if (!Enum.TryParse<Gender>(updateDto.Gender, true, out _))
            {
                return Result.Fail("Invalid Gender value");
            }
        }

        // Валидация ActivityLevel enum
        if (!string.IsNullOrEmpty(updateDto.ActivityLevel))
        {
            if (!Enum.TryParse<ActivityLevel>(updateDto.ActivityLevel, true, out _))
            {
                return Result.Fail("Invalid ActivityLevel value");
            }
        }

        // Валидация FitnessGoal enum
        if (string.IsNullOrEmpty(updateDto.FitnessGoal))
            return Result.Ok();

        return !Enum.TryParse<FitnessGoal>(updateDto.FitnessGoal, true, out _)
            ? Result.Fail("Invalid FitnessGoal value")
            : Result.Ok();
    }

    private static void UpdateProfileFields(
        UserProfile profile,
        UpdateUserProfileDto? updateDto
    )
    {
        // Обновляем только те поля, которые переданы (не null)
        if (updateDto!.Weight.HasValue)
        {
            profile.Weight = updateDto.Weight.Value;
        }

        if (updateDto.Height.HasValue)
        {
            profile.Height = updateDto.Height.Value;
        }

        if (updateDto.Age.HasValue)
        {
            profile.Age = updateDto.Age.Value;
        }

        if (!string.IsNullOrEmpty(updateDto.Gender))
        {
            profile.Gender = Enum.Parse<Gender>(updateDto.Gender, true);
        }

        if (!string.IsNullOrEmpty(updateDto.ActivityLevel))
        {
            profile.ActivityLevel = Enum.Parse<ActivityLevel>(
                updateDto.ActivityLevel,
                true
            );
        }

        if (!string.IsNullOrEmpty(updateDto.FitnessGoal))
        {
            profile.FitnessGoal = Enum.Parse<FitnessGoal>(
                updateDto.FitnessGoal,
                true
            );
        }
    }
}
