using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entity;

namespace User.Application.Database.EntityConfiguration;

public class UserDbConfiguration : IEntityTypeConfiguration<Domain.Entity.UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.HasKey(user => user.Id);
        builder
            .HasOne<UserSettings>()
            .WithOne()
            .HasForeignKey<UserSettings>(user => user.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<UserProfile>()
            .WithOne()
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany<OneTimePassword>()
            .WithOne()
            .HasForeignKey(password => password.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany<AccessToken>()
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany<RefreshToken>()
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
