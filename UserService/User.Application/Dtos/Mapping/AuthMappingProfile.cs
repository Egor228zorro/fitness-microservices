using AutoMapper;
using User.Domain.Entity;
using User.Domain.Enum;

namespace User.Application.Dtos.Mapping;

public class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        // UserEntity mappings
        this.CreateMap<RegisterUserDto, UserEntity>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => Role.Guest))
            .ForMember(
                dest => dest.CreatedAt,
                opt => opt.MapFrom(src => DateTime.UtcNow)
            )
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

        // OneTimePassword mappings
        this.CreateMap<VerifyOtpRequestDto, OneTimePassword>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.OtpCode, opt => opt.MapFrom(src => src.OtpCode))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ExpiresAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsUsed, opt => opt.Ignore());

        // Reverse mappings for reading operations
        this.CreateMap<OneTimePassword, VerifyOtpRequestDto>()
            .ForCtorParam("Email", opt => opt.MapFrom(_ => (string)null!)) // Явное указание null
            .ForCtorParam("OtpCode", opt => opt.MapFrom(src => src.OtpCode));
        // Enum to string conversions
        this.CreateMap<Role, string>().ConvertUsing(src => src.ToString());

        // String to enum conversions (for parsing)
        this.CreateMap<string, Role>()
            .ConvertUsing(src => Enum.Parse<Role>(src, true));
    }
}
