using FluentValidation;
using Kalora.Application.Auth;
using Kalora.Application.Availability;
using Kalora.Application.Catalog;
using Kalora.Application.Email;
using Kalora.Application.PublicBooking;
using Kalora.Application.Staff;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Ping;
using Kalora.Application.Salons;
using Kalora.Application.Security;
using Kalora.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kalora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<AppOptions>()
            .BindConfiguration(AppOptions.SectionName)
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();
        services.AddOptions<MailOptions>()
            .BindConfiguration(MailOptions.SectionName)
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<MailOptions>, MailOptionsValidator>();
        services.AddSingleton<IManageTokens, ManageTokens>();
        services.AddSingleton<IEmailRenderer, EmailRenderer>();
        services.AddScoped<IEmailOutboxProcessor, EmailOutboxProcessor>();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ISalonResolver, SalonResolver>();

        services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddSingleton<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddSingleton<IValidator<CreateInvitationRequest>, CreateInvitationRequestValidator>();
        services.AddSingleton<IValidator<AcceptInvitationRequest>, AcceptInvitationRequestValidator>();

        services.AddScoped<IEmailOutbox, EmailOutboxWriter>();
        services.AddScoped<MeResponseFactory>();
        services.AddSingleton<IValidator<SalonProfile>, SalonProfileValidator>();
        services.AddSingleton<IValidator<BookingRules>, BookingRulesValidator>();
        services.AddSingleton<IValidator<OpeningHoursPlan>, OpeningHoursPlanValidator>();
        services.AddSingleton<IValidator<CreateClosureRequest>, CreateClosureRequestValidator>();
        services.AddSingleton<IValidator<SaveCategoryRequest>, SaveCategoryRequestValidator>();
        services.AddSingleton<IValidator<SaveServiceRequest>, SaveServiceRequestValidator>();
        services.AddSingleton<IValidator<ApplyTemplateRequest>, ApplyTemplateRequestValidator>();
        services.AddSingleton<IValidator<CreateStaffRequest>, CreateStaffRequestValidator>();
        services.AddSingleton<IValidator<SaveStaffRequest>, SaveStaffRequestValidator>();
        services.AddSingleton<IValidator<StaffSkills>, StaffSkillsValidator>();
        services.AddSingleton<IValidator<CreateAbsenceRequest>, CreateAbsenceRequestValidator>();
        services.AddSingleton<IValidator<CreateAppointmentRequest>, CreateAppointmentRequestValidator>();
        services.AddScoped<IPublicSalonService, PublicSalonService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IStaffService, StaffManagementService>();
        services.AddScoped<IStaffScheduleService, StaffScheduleService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ISlugAllocator, SlugAllocator>();
        services.AddScoped<ISalonSettingsService, SalonSettingsService>();
        services.AddScoped<IOpeningHoursService, OpeningHoursService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IInvitationService, InvitationService>();

        services.AddScoped<IPingService, PingService>();
        return services;
    }
}
