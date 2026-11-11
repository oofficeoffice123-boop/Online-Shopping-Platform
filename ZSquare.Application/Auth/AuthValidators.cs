using FluentValidation;
namespace ZSquare.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)")
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter, and one digit.");
        RuleFor(request => request.Phone).MaximumLength(30);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty();
    }
}
