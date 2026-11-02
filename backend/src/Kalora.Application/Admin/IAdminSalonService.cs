using Kalora.Application.Common;
using Kalora.Domain.Salons;

namespace Kalora.Application.Admin;

/// <summary>What the platform admin does with the salons: look them up and set plan, trial, status and note. Platform admins only.</summary>
public interface IAdminSalonService
{
    Task<Result<AdminSalonList>> ListAsync(string? search, SalonStatus? status, CancellationToken cancellationToken = default);

    Task<Result<AdminSalonDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AdminSalonDetail>> SetPlanAsync(Guid id, SetPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>Moves the end of the trial: from the later of now and the current end. Only for salons in the trial.</summary>
    Task<Result<AdminSalonDetail>> ExtendTrialAsync(Guid id, ExtendTrialRequest request, CancellationToken cancellationToken = default);

    Task<Result<AdminSalonDetail>> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken cancellationToken = default);

    Task<Result<AdminSalonDetail>> SuspendAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Back to the trial if it is still running, otherwise to active.</summary>
    Task<Result<AdminSalonDetail>> UnsuspendAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AdminSalonDetail>> SetNoteAsync(Guid id, SetNoteRequest request, CancellationToken cancellationToken = default);
}
