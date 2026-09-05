using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Printing;

namespace OFC.Api.Features;

public static class SprintNineEndpoints
{
    public static void MapSprintNineEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/print/stations", ListStations).RequireAuthorization();
        api.MapGet("/print/configs", ListConfigs).RequireAuthorization();
        api.MapPost("/print/configs", CreateConfig).RequireAuthorization();
        api.MapPut("/print/configs/{id:guid}", UpdateConfig).RequireAuthorization();
        api.MapGet("/print/templates", ListTemplates).RequireAuthorization();
        api.MapPost("/print/templates", CreateTemplate).RequireAuthorization();
        api.MapPut("/print/templates/{id:guid}", UpdateTemplate).RequireAuthorization();
        api.MapGet("/print/routes", ListRoutes).RequireAuthorization();
        api.MapPost("/print/routes", CreateRoute).RequireAuthorization();
        api.MapPut("/print/routes/{id:guid}", UpdateRoute).RequireAuthorization();
        api.MapGet("/print/jobs", ListJobs).RequireAuthorization();
        api.MapPost("/print/jobs", Enqueue).RequireAuthorization();
        api.MapPost("/print/jobs/{id:guid}/claim", Claim).RequireAuthorization();
        api.MapPost("/print/jobs/{id:guid}/complete", Complete).RequireAuthorization();
        api.MapPost("/print/jobs/{id:guid}/fail", Fail).RequireAuthorization();
        api.MapPost("/print/jobs/{id:guid}/retry", Retry).RequireAuthorization();
    }

    private static async Task<IResult> ListStations(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanPrint(user) || !user.HasClaim("permission", "printing.view")) return Forbidden();
        return Results.Ok(await db.PreparationStations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn }).ToListAsync(ct));
    }

    private static async Task<IResult> ListConfigs(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !user.HasClaim("permission", "printing.view")) return Forbidden();
        return Results.Ok(await db.PrinterConfigurations.AsNoTracking().Where(x => x.BranchId == branchId).OrderBy(x => x.SortOrder).ThenBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.Kind, x.DeviceName, x.IsActive, x.SortOrder }).ToListAsync(ct));
    }

    private static async Task<IResult> CreateConfig(ConfigRequest request, Guid branchId, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !user.HasClaim("permission", "printing.configs.manage")) return Forbidden();
        if (!ValidConfig(request)) return Validation("config", "Provide a printer code, names, a valid kind, and a valid sort order.");
        if (await db.PrinterConfigurations.AnyAsync(x => x.BranchId == branchId && x.Code == request.Code.Trim(), ct)) return Validation("code", "A printer configuration with this code already exists at this branch.");
        var config = new PrinterConfiguration { BranchId = branchId, Code = request.Code.Trim(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), Kind = request.Kind, DeviceName = request.DeviceName?.Trim(), IsActive = request.IsActive ?? true, SortOrder = request.SortOrder ?? 0 };
        db.PrinterConfigurations.Add(config);
        identity.Audit(UserId(user), branchId, DeviceId(user), "printing.config.create", "printer_configuration", config.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { config.Code, config.NameAr, config.NameEn, config.Kind, config.DeviceName, config.IsActive, config.SortOrder }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/print/configs/{config.Id}", ConfigResponse(config));
    }

    private static async Task<IResult> UpdateConfig(Guid id, ConfigUpdateRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var config = await db.PrinterConfigurations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (config is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, config.BranchId, ct) || !user.HasClaim("permission", "printing.configs.manage")) return Forbidden();
        if (request.NameAr?.Trim().Length is > PrintingRules.NameMax || request.NameEn?.Trim().Length is > PrintingRules.NameMax || request.DeviceName?.Trim().Length is > PrintingRules.DeviceNameMax || request.SortOrder is < 0 or > PrintingRules.SortOrderMax) return Validation("config", "Printer configuration fields exceed the allowed length or sort order range.");
        if (request.Kind is not null) config.Kind = request.Kind.Value;
        if (request.NameAr is not null) config.NameAr = request.NameAr.Trim();
        if (request.NameEn is not null) config.NameEn = request.NameEn.Trim();
        if (request.DeviceName is not null) config.DeviceName = request.DeviceName.Trim();
        if (request.SortOrder is not null) config.SortOrder = request.SortOrder.Value;
        if (request.IsActive is not null) config.IsActive = request.IsActive.Value;
        identity.Audit(UserId(user), config.BranchId, DeviceId(user), "printing.config.update", "printer_configuration", config.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { config.Code, config.NameAr, config.NameEn, config.Kind, config.DeviceName, config.IsActive, config.SortOrder }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(ConfigResponse(config));
    }

    private static async Task<IResult> ListTemplates(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !user.HasClaim("permission", "printing.view")) return Forbidden();
        return Results.Ok(await db.PrintTemplates.AsNoTracking().Where(x => x.BranchId == branchId).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn, x.Kind, x.WidthChars, x.Content, x.IsActive }).ToListAsync(ct));
    }

    private static async Task<IResult> CreateTemplate(TemplateRequest request, Guid branchId, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !user.HasClaim("permission", "printing.templates.manage")) return Forbidden();
        if (!ValidTemplate(request)) return Validation("template", "Provide a template code, names, a valid kind, width between 20 and 120, and content.");
        if (await db.PrintTemplates.AnyAsync(x => x.BranchId == branchId && x.Code == request.Code.Trim(), ct)) return Validation("code", "A print template with this code already exists at this branch.");
        var template = new PrintTemplate { BranchId = branchId, Code = request.Code.Trim(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), Kind = request.Kind, WidthChars = request.WidthChars ?? 42, Content = request.Content.Trim(), IsActive = request.IsActive ?? true };
        db.PrintTemplates.Add(template);
        identity.Audit(UserId(user), branchId, DeviceId(user), "printing.template.create", "print_template", template.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { template.Code, template.NameAr, template.NameEn, template.Kind, template.WidthChars, template.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/print/templates/{template.Id}", TemplateResponse(template));
    }

    private static async Task<IResult> UpdateTemplate(Guid id, TemplateUpdateRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var template = await db.PrintTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, template.BranchId, ct) || !user.HasClaim("permission", "printing.templates.manage")) return Forbidden();
        if (request.NameAr?.Trim().Length is > PrintingRules.NameMax || request.NameEn?.Trim().Length is > PrintingRules.NameMax || request.Content?.Trim().Length is > PrintingRules.TemplateContentMax || !PrintingRules.ValidWidth(request.WidthChars ?? template.WidthChars)) return Validation("template", "Template fields exceed the allowed length or width range.");
        if (request.Kind is not null) template.Kind = request.Kind.Value;
        if (request.NameAr is not null) template.NameAr = request.NameAr.Trim();
        if (request.NameEn is not null) template.NameEn = request.NameEn.Trim();
        if (request.WidthChars is not null) template.WidthChars = request.WidthChars.Value;
        if (request.Content is not null) template.Content = request.Content.Trim();
        if (request.IsActive is not null) template.IsActive = request.IsActive.Value;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), template.BranchId, DeviceId(user), "printing.template.update", "print_template", template.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { template.Code, template.NameAr, template.NameEn, template.Kind, template.WidthChars, template.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(TemplateResponse(template));
    }

    private static async Task<IResult> ListRoutes(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !user.HasClaim("permission", "printing.view")) return Forbidden();
        var routes = await db.PrinterRoutes.AsNoTracking().Where(x => x.BranchId == branchId).OrderBy(x => x.Priority).ToListAsync(ct);
        var configIds = routes.Select(x => x.PrinterConfigurationId).Distinct().ToList();
        var templateIds = routes.Select(x => x.PrintTemplateId).Distinct().ToList();
        var stationIds = routes.Select(x => x.PreparationStationId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var configs = configIds.Count == 0 ? new Dictionary<Guid, PrinterConfiguration>() : await db.PrinterConfigurations.AsNoTracking().Where(x => configIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var templates = templateIds.Count == 0 ? new Dictionary<Guid, PrintTemplate>() : await db.PrintTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var stations = stationIds.Count == 0 ? new Dictionary<Guid, PreparationStation>() : await db.PreparationStations.AsNoTracking().Where(x => stationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(routes.Select(route =>
        {
            configs.TryGetValue(route.PrinterConfigurationId, out var config);
            templates.TryGetValue(route.PrintTemplateId, out var template);
            var station = route.PreparationStationId is Guid sid && stations.TryGetValue(sid, out var found) ? found : null;
            return new { route.Id, route.BranchId, route.PreparationStationId, stationCode = station == null ? (string?)null : station.Code, stationNameAr = station == null ? null : station.NameAr, stationNameEn = station == null ? null : station.NameEn, route.PrinterConfigurationId, printerCode = config == null ? (string?)null : config.Code, printerNameAr = config == null ? null : config.NameAr, printerNameEn = config == null ? null : config.NameEn, route.PrintTemplateId, templateCode = template == null ? (string?)null : template.Code, route.Priority, route.IsActive };
        }));
    }

    private static async Task<IResult> CreateRoute(RouteRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, request.BranchId, ct) || !user.HasClaim("permission", "printing.routes.manage")) return Forbidden();
        if (request.Priority is < 1 or > PrintingRules.PriorityMax) return Validation("priority", "Route priority must be between 1 and 999.");
        if (!await db.PrinterConfigurations.AnyAsync(x => x.Id == request.PrinterConfigurationId && x.BranchId == request.BranchId, ct)) return Validation("printerConfigurationId", "The selected printer configuration is invalid at this branch.");
        if (!await db.PrintTemplates.AnyAsync(x => x.Id == request.PrintTemplateId && x.BranchId == request.BranchId, ct)) return Validation("printTemplateId", "The selected print template is invalid at this branch.");
        if (await db.PrinterRoutes.CountAsync(x => x.BranchId == request.BranchId, ct) >= PrintingRules.RouteCountMax) return Validation("routes", "Too many printer routes for this branch.");
        var route = new PrinterRoute { BranchId = request.BranchId, PreparationStationId = request.PreparationStationId, PrinterConfigurationId = request.PrinterConfigurationId, PrintTemplateId = request.PrintTemplateId, Priority = request.Priority ?? 1, IsActive = request.IsActive ?? true };
        db.PrinterRoutes.Add(route);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "printing.route.create", "printer_route", route.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { route.BranchId, route.PreparationStationId, route.PrinterConfigurationId, route.PrintTemplateId, route.Priority, route.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/print/routes/{route.Id}", new { route.Id, route.BranchId, route.PreparationStationId, route.PrinterConfigurationId, route.PrintTemplateId, route.Priority, route.IsActive });
    }

    private static async Task<IResult> UpdateRoute(Guid id, RouteUpdateRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var route = await db.PrinterRoutes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (route is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, route.BranchId, ct) || !user.HasClaim("permission", "printing.routes.manage")) return Forbidden();
        if (request.Priority is < 1 or > PrintingRules.PriorityMax) return Validation("priority", "Route priority must be between 1 and 999.");
        if (request.PrinterConfigurationId is not null && !await db.PrinterConfigurations.AnyAsync(x => x.Id == request.PrinterConfigurationId && x.BranchId == route.BranchId, ct)) return Validation("printerConfigurationId", "The selected printer configuration is invalid at this branch.");
        if (request.PrintTemplateId is not null && !await db.PrintTemplates.AnyAsync(x => x.Id == request.PrintTemplateId && x.BranchId == route.BranchId, ct)) return Validation("printTemplateId", "The selected print template is invalid at this branch.");
        if (request.PreparationStationId is not null) route.PreparationStationId = request.PreparationStationId;
        if (request.PrinterConfigurationId is not null) route.PrinterConfigurationId = request.PrinterConfigurationId.Value;
        if (request.PrintTemplateId is not null) route.PrintTemplateId = request.PrintTemplateId.Value;
        if (request.Priority is not null) route.Priority = request.Priority.Value;
        if (request.IsActive is not null) route.IsActive = request.IsActive.Value;
        identity.Audit(UserId(user), route.BranchId, DeviceId(user), "printing.route.update", "printer_route", route.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { route.BranchId, route.PreparationStationId, route.PrinterConfigurationId, route.PrintTemplateId, route.Priority, route.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { route.Id, route.BranchId, route.PreparationStationId, route.PrinterConfigurationId, route.PrintTemplateId, route.Priority, route.IsActive });
    }

    private static async Task<IResult> ListJobs(Guid branchId, bool? actionable, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, branchId, ct) || !(user.HasClaim("permission", "printing.jobs.manage") || user.HasClaim("permission", "printing.view"))) return Forbidden();
        var query = db.PrintJobs.AsNoTracking().Where(x => x.BranchId == branchId);
        if (actionable == true)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(x => x.Status == PrintJobStatus.Pending || x.Status == PrintJobStatus.Retrying || (x.Status == PrintJobStatus.Failed && x.AttemptCount < x.MaxAttempts && (x.NextAttemptAt == null || x.NextAttemptAt <= now)));
        }
        return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Take(200).Select(x => new { x.Id, x.BranchId, x.DeviceId, x.OrderId, x.Kind, x.Status, x.PrinterConfigurationId, x.TemplateCode, x.Payload, x.AttemptCount, x.MaxAttempts, x.NextAttemptAt, x.LastError, x.PrintedAt, x.CreatedAt, x.CreatedByUserId }).ToListAsync(ct));
    }

    private static async Task<IResult> Enqueue(EnqueueRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanPrintAt(db, user, request.BranchId, ct) || !user.HasClaim("permission", "printing.jobs.manage")) return Forbidden();
        if (!Enum.IsDefined(request.Kind) || request.ClientRequestId == Guid.Empty) return Validation("job", "Provide a valid print job kind and client request id.");
        if (request.TemplateCode?.Trim().Length is > PrintingRules.CodeMax) return Validation("templateCode", "The template code is too long.");
        if (await db.PrintJobs.AnyAsync(x => x.BranchId == request.BranchId && x.ClientRequestId == request.ClientRequestId && x.Kind == request.Kind, ct)) return Results.Ok(new { enqueued = false, duplicate = true });

        var routes = await db.PrinterRoutes.AsNoTracking().Where(x => x.BranchId == request.BranchId).ToListAsync(ct);
        var route = PrintingRules.PickRoute(routes, request.PreparationStationId);
        Guid? printerConfigId = route?.PrinterConfigurationId;
        string? templateCode = request.TemplateCode?.Trim();
        if (route is not null && templateCode is null)
            templateCode = await db.PrintTemplates.AsNoTracking().Where(x => x.Id == route.PrintTemplateId).Select(x => x.Code).FirstOrDefaultAsync(ct);

        var job = new PrintJob
        {
            BranchId = request.BranchId,
            DeviceId = DeviceId(user),
            OrderId = request.OrderId,
            ClientRequestId = request.ClientRequestId,
            Kind = request.Kind,
            Status = PrintJobStatus.Pending,
            PrinterConfigurationId = printerConfigId,
            TemplateCode = templateCode,
            Payload = request.Payload is null ? "{}" : request.Payload.Value.GetRawText(),
            CreatedByUserId = UserId(user)
        };
        db.PrintJobs.Add(job);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "printing.job.enqueue", "print_job", job.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { job.Kind, job.OrderId, job.TemplateCode, route = route is null ? (Guid?)null : route.Id }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/print/jobs/{job.Id}", new { job.Id, job.BranchId, job.Kind, job.Status, job.PrinterConfigurationId, job.TemplateCode, job.CreatedAt, enqueued = true, duplicate = false });
    }

    private static async Task<IResult> Claim(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, job.BranchId, ct) || !user.HasClaim("permission", "printing.jobs.manage")) return Forbidden();
        if (job.Status == PrintJobStatus.Failed && !PrintingRules.ShouldRetry(job, DateTimeOffset.UtcNow)) return Validation("job", "This job is no longer retryable.");
        if (job.Status != PrintJobStatus.Pending && job.Status != PrintJobStatus.Retrying && !(job.Status == PrintJobStatus.Failed && PrintingRules.ShouldRetry(job, DateTimeOffset.UtcNow))) return Validation("job", "This job is not ready to be printed.");
        if (job.Status == PrintJobStatus.Retrying && job.NextAttemptAt is not null && DateTimeOffset.UtcNow < job.NextAttemptAt.Value) return Validation("job", "The retry is not due yet.");
        if (!PrintingRules.CanTransition(job.Status, PrintJobStatus.Printing)) return Validation("job", "This job cannot enter the printing state.");
        job.Status = PrintJobStatus.Printing; job.AttemptCount += 1; job.NextAttemptAt = null; job.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), job.BranchId, DeviceId(user), "printing.job.claim", "print_job", job.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { job.Status, job.AttemptCount }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(JobResponse(job));
    }

    private static async Task<IResult> Complete(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, job.BranchId, ct) || !user.HasClaim("permission", "printing.jobs.manage")) return Forbidden();
        if (!PrintingRules.CanTransition(job.Status, PrintJobStatus.Printed)) return Validation("job", "Only a job being printed can be marked complete.");
        job.Status = PrintJobStatus.Printed; job.PrintedAt = DateTimeOffset.UtcNow; job.LastError = null; job.NextAttemptAt = null; job.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), job.BranchId, DeviceId(user), "printing.job.complete", "print_job", job.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { job.Status, job.PrintedAt, job.AttemptCount }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(JobResponse(job));
    }

    private static async Task<IResult> Fail(Guid id, FailRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, job.BranchId, ct) || !user.HasClaim("permission", "printing.jobs.manage")) return Forbidden();
        if (job.Status != PrintJobStatus.Printing) return Validation("job", "Only a job being printed can be reported as failed.");
        if (request.Error?.Trim().Length is > PrintingRules.JobErrorMax) return Validation("error", "The failure reason is too long.");
        var retry = job.AttemptCount < job.MaxAttempts;
        job.Status = retry ? PrintJobStatus.Retrying : PrintJobStatus.Failed;
        job.LastError = request.Error?.Trim();
        job.NextAttemptAt = retry ? DateTimeOffset.UtcNow.Add(PrintingRules.RetryBackoff(job.AttemptCount)) : null;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), job.BranchId, DeviceId(user), "printing.job.fail", "print_job", job.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { job.Status, job.AttemptCount, job.NextAttemptAt, job.LastError }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(JobResponse(job, retry));
    }

    private static async Task<IResult> Retry(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Results.NotFound();
        if (!await CanPrintAt(db, user, job.BranchId, ct) || !user.HasClaim("permission", "printing.jobs.manage")) return Forbidden();
        if (!PrintingRules.CanTransition(job.Status, PrintJobStatus.Retrying)) return Validation("job", "Only a failed job can be retried.");
        job.Status = PrintJobStatus.Retrying; job.NextAttemptAt = DateTimeOffset.UtcNow; job.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), job.BranchId, DeviceId(user), "printing.job.retry", "print_job", job.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { job.Status, job.AttemptCount, job.NextAttemptAt }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(JobResponse(job));
    }

    private static object ConfigResponse(PrinterConfiguration config) => new { config.Id, config.BranchId, config.Code, config.NameAr, config.NameEn, config.Kind, config.DeviceName, config.IsActive, config.SortOrder };
    private static object TemplateResponse(PrintTemplate template) => new { template.Id, template.BranchId, template.Code, template.NameAr, template.NameEn, template.Kind, template.WidthChars, template.IsActive };
    private static object JobResponse(PrintJob job, bool? retry = null) => new { job.Id, job.BranchId, job.Kind, job.Status, job.PrinterConfigurationId, job.TemplateCode, job.AttemptCount, job.MaxAttempts, job.NextAttemptAt, job.LastError, job.PrintedAt, job.CreatedAt, retry };

    private static bool ValidConfig(ConfigRequest request) => !string.IsNullOrWhiteSpace(request.Code) && request.Code.Trim().Length <= PrintingRules.CodeMax && !string.IsNullOrWhiteSpace(request.NameAr) && request.NameAr.Trim().Length <= PrintingRules.NameMax && !string.IsNullOrWhiteSpace(request.NameEn) && request.NameEn.Trim().Length <= PrintingRules.NameMax && Enum.IsDefined(request.Kind) && request.DeviceName?.Trim().Length <= PrintingRules.DeviceNameMax && (request.SortOrder ?? 0) is >= 0 and <= PrintingRules.SortOrderMax;
    private static bool ValidTemplate(TemplateRequest request) => !string.IsNullOrWhiteSpace(request.Code) && request.Code.Trim().Length <= PrintingRules.CodeMax && !string.IsNullOrWhiteSpace(request.NameAr) && request.NameAr.Trim().Length <= PrintingRules.NameMax && !string.IsNullOrWhiteSpace(request.NameEn) && request.NameEn.Trim().Length <= PrintingRules.NameMax && Enum.IsDefined(request.Kind) && PrintingRules.ValidWidth(request.WidthChars ?? 42) && !string.IsNullOrWhiteSpace(request.Content) && request.Content.Trim().Length <= PrintingRules.TemplateContentMax;
    private static bool CanPrint(ClaimsPrincipal user) => user.HasClaim("permission", "printing.configs.manage") || user.HasClaim("permission", "printing.templates.manage") || user.HasClaim("permission", "printing.routes.manage") || user.HasClaim("permission", "printing.jobs.manage") || user.HasClaim("permission", "printing.view");
    private static async Task<bool> CanPrintAt(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record ConfigRequest(string Code, string NameAr, string NameEn, PrinterKind Kind, string? DeviceName, int? SortOrder, bool? IsActive);
    private sealed record ConfigUpdateRequest(string? NameAr, string? NameEn, PrinterKind? Kind, string? DeviceName, int? SortOrder, bool? IsActive);
    private sealed record TemplateRequest(string Code, string NameAr, string NameEn, PrinterKind Kind, int? WidthChars, string Content, bool? IsActive);
    private sealed record TemplateUpdateRequest(string? NameAr, string? NameEn, PrinterKind? Kind, int? WidthChars, string? Content, bool? IsActive);
    private sealed record RouteRequest(Guid BranchId, Guid? PreparationStationId, Guid PrinterConfigurationId, Guid PrintTemplateId, int? Priority, bool? IsActive);
    private sealed record RouteUpdateRequest(Guid? PreparationStationId, Guid? PrinterConfigurationId, Guid? PrintTemplateId, int? Priority, bool? IsActive);
    private sealed record EnqueueRequest(Guid BranchId, Guid? OrderId, Guid ClientRequestId, PrintJobKind Kind, Guid? PreparationStationId, string? TemplateCode, JsonElement? Payload);
    private sealed record FailRequest(string? Error);
}
