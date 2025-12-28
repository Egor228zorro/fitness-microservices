using AutoMapper;
using PublicWorkout.Domain.Entity;

namespace PublicWorkout.Application.Dtos.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Workout -> PublicWorkoutDto
        // this.CreateMap<Workout, PublicWorkoutDto>()
        //     .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
        //     .ForMember(
        //         dest => dest.AuthorId,
        //         opt => opt.MapFrom(src => src.AuthorId)
        //     )
        //     .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
        //     .ForMember(dest => dest.WorkoutType, opt => opt.MapFrom(src => src.Type))
        //     .ForMember(
        //         dest => dest.PreviewUrl,
        //         opt => opt.MapFrom(src => src.PreviewUrl)
        //     )
        //     .ForMember(
        //         dest => dest.LikesCount,
        //         opt => opt.MapFrom(src => src.LikesCount)
        //     )
        //     .ForMember(
        //         dest => dest.CopiesCount,
        //         opt => opt.MapFrom(src => src.CopiesCount)
        //     )
        //     .ForMember(
        //         dest => dest.CreatedAt,
        //         opt => opt.MapFrom(src => src.CreatedAt)
        //     );

        // Workout -> PublicWorkoutDetailDto (с упражнениями)
        // this.CreateMap<Workout, PublicWorkoutDetailDto>()
        //     .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
        //     .ForMember(
        //         dest => dest.AuthorId,
        //         opt => opt.MapFrom(src => src.AuthorId)
        //     )
        //     .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
        //     .ForMember(dest => dest.WorkoutType, opt => opt.MapFrom(src => src.WorkoutType))
        //     .ForMember(
        //         dest => dest.PreviewUrl,
        //         opt => opt.MapFrom(src => src.PreviewUrl)
        //     )
        //     .ForMember(
        //         dest => dest.LikesCount,
        //         opt => opt.MapFrom(src => src.LikesCount)
        //     )
        //     .ForMember(
        //         dest => dest.CopiesCount,
        //         opt => opt.MapFrom(src => src.CopiesCount)
        //     )
        //     .ForMember(
        //         dest => dest.CreatedAt,
        //         opt => opt.MapFrom(src => src.CreatedAt)
        //     )
        //     .ForMember(dest => dest.Exercises, opt => opt.Ignore()); // Упражнения добавляются отдельно в сервисе

        // Workout -> PrivateWorkoutDto (для копирования)
        this.CreateMap<Workout, PrivateWorkoutDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid())) // Новый ID для приватной копии
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(
                dest => dest.CreatedAt,
                opt => opt.MapFrom(src => DateTime.UtcNow)
            ); // Новое время создания

        // Exercise -> ExerciseDto
        this.CreateMap<Exercise, ExerciseDto>()
            .ForMember(
                dest => dest.ExerciseId,
                opt => opt.MapFrom(src => src.ExerciseId)
            )
            .ForMember(
                dest => dest.OrderIndex,
                opt => opt.MapFrom(src => src.OrderIndex)
            )
            .ForMember(
                dest => dest.DurationSeconds,
                opt => opt.MapFrom(src => src.DurationSeconds)
            );

        // Comment -> CommentDto
        this.CreateMap<Comment, CommentDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(
                dest => dest.ParentCommentId,
                opt => opt.MapFrom(src => src.ParentCommentId)
            )
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(
                dest => dest.CreatedAt,
                opt => opt.MapFrom(src => src.CreatedAt)
            )
            .ForMember(
                dest => dest.UpdatedAt,
                opt => opt.MapFrom(src => src.UpdatedAt)
            );

        // AddCommentDto -> Comment
        this.CreateMap<AddCommentDto, Comment>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.UserId, opt => opt.Ignore()) // Устанавливается в сервисе из контекста пользователя
            .ForMember(dest => dest.WorkoutId, opt => opt.Ignore()) // Устанавливается в сервисе из параметра эндпоинта
            .ForMember(
                dest => dest.ParentCommentId,
                opt => opt.MapFrom(src => src.ParentCommentId)
            )
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(
                dest => dest.CreatedAt,
                opt => opt.MapFrom(src => DateTime.UtcNow)
            )
            .ForMember(
                dest => dest.UpdatedAt,
                opt => opt.MapFrom(src => (DateTime?)null)
            );

        // UpdateCommentDto -> Comment
        this.CreateMap<UpdateCommentDto, Comment>()
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(
                dest => dest.UpdatedAt,
                opt => opt.MapFrom(src => DateTime.UtcNow)
            )
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkoutId, opt => opt.Ignore())
            .ForMember(dest => dest.ParentCommentId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
    }
}
