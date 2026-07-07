// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Utilities;

namespace AeonDocGen.Core.Validators;

/// <summary>
/// Reusable validation logic for document review request payloads
/// without changing documented request schemas.
/// </summary>
public static class DocumentReviewRequestValidator
{
    private static readonly HashSet<string> ValidActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "submit", "startReview", "approve", "reject"
    };

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public static readonly Dictionary<string, (string[] AllowedFrom, string ResultStatus)> TransitionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["submit"] = (new[] { "draft" }, "draft"),
            ["startReview"] = (new[] { "draft" }, "inReview"),
            ["approve"] = (new[] { "inReview" }, "approved"),
            ["reject"] = (new[] { "inReview" }, "rejected")
        };

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public static List<string> Validate(DocumentReviewRequestDto request, int maxCommentsLength = 2000)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            errors.Add("action is required.");
            return errors;
        }

        if (!ValidActions.Contains(request.Action))
        {
            errors.Add($"action must be one of: {string.Join(", ", ValidActions)}.");
        }

        if (request.Comments != null && request.Comments.Length > maxCommentsLength)
        {
            errors.Add($"comments length must not exceed {maxCommentsLength} characters.");
        }

        return errors;
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public static (bool IsValid, string? ErrorMessage) ValidateStateTransition(
        string action, string currentStatus)
    {
        if (!TransitionMap.TryGetValue(action, out var transition))
        {
            return (false, $"action must be one of: {string.Join(", ", ValidActions)}.");
        }

        if (!transition.AllowedFrom.Contains(currentStatus, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"The requested review action '{action}' is invalid for the current document review status '{currentStatus}'.");
        }

        return (true, null);
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public static (bool IsValid, StandardErrorDto? Error) ValidateRouteParameters(
        string? projectId, string? documentId, string traceId)
    {
        if (!OpaqueIdentifier.TryNormalize(projectId, "project", out _))
        {
            return (false, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "INVALID_REQUEST_BODY",
                Message = "projectId must be a non-empty identifier."
            });
        }

        if (!OpaqueIdentifier.TryNormalize(documentId, "document", out _))
        {
            return (false, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "INVALID_REQUEST_BODY",
                Message = "documentId must be a non-empty identifier."
            });
        }

        return (true, null);
    }
}
