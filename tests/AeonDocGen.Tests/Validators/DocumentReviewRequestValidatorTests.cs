// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Validators;

namespace AeonDocGen.Tests.Validators;

public class DocumentReviewRequestValidatorTests
{
    // Request validation
    [Theory]
    [InlineData("submit")]
    [InlineData("startReview")]
    [InlineData("approve")]
    [InlineData("reject")]
    public void Validate_ValidActions_NoErrors(string action)
    {
        var request = new DocumentReviewRequestDto { Action = action };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyAction_ReturnsError()
    {
        var request = new DocumentReviewRequestDto { Action = "" };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Contains(errors, e => e.Contains("action is required"));
    }

    [Fact]
    public void Validate_NullAction_ReturnsError()
    {
        var request = new DocumentReviewRequestDto { Action = null! };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Contains(errors, e => e.Contains("action is required"));
    }

    [Fact]
    public void Validate_InvalidAction_ReturnsError()
    {
        var request = new DocumentReviewRequestDto { Action = "invalidAction" };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Contains(errors, e => e.Contains("action must be one of"));
    }

    [Fact]
    public void Validate_CommentsTooLong_ReturnsError()
    {
        var request = new DocumentReviewRequestDto
        {
            Action = "approve",
            Comments = new string('X', 2001)
        };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Contains(errors, e => e.Contains("comments length must not exceed"));
    }

    [Fact]
    public void Validate_CommentsAtLimit_NoError()
    {
        var request = new DocumentReviewRequestDto
        {
            Action = "approve",
            Comments = new string('X', 2000)
        };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NullComments_NoError()
    {
        var request = new DocumentReviewRequestDto
        {
            Action = "submit",
            Comments = null
        };
        var errors = DocumentReviewRequestValidator.Validate(request);
        Assert.Empty(errors);
    }

    // State transition validation
    [Fact]
    public void ValidateStateTransition_SubmitFromDraft_Valid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("submit", "draft");
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateStateTransition_StartReviewFromDraft_Valid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("startReview", "draft");
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateStateTransition_ApproveFromInReview_Valid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("approve", "inReview");
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateStateTransition_RejectFromInReview_Valid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("reject", "inReview");
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateStateTransition_ApproveFromDraft_Invalid()
    {
        var (isValid, errorMsg) = DocumentReviewRequestValidator.ValidateStateTransition("approve", "draft");
        Assert.False(isValid);
        Assert.Contains("invalid for the current document review status", errorMsg!);
    }

    [Fact]
    public void ValidateStateTransition_RejectFromDraft_Invalid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("reject", "draft");
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateStateTransition_SubmitFromInReview_Invalid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("submit", "inReview");
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateStateTransition_StartReviewFromApproved_Invalid()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateStateTransition("startReview", "approved");
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateStateTransition_InvalidAction_Invalid()
    {
        var (isValid, errorMsg) = DocumentReviewRequestValidator.ValidateStateTransition("delete", "draft");
        Assert.False(isValid);
        Assert.Contains("action must be one of", errorMsg!);
    }

    // Route parameter validation
    [Fact]
    public void ValidateRouteParameters_OpaqueIds_ReturnsValid()
    {
        var (isValid, error) = DocumentReviewRequestValidator.ValidateRouteParameters(
            "prj-001", "doc-001", "trace-1");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateRouteParameters_EmptyProjectId_ReturnsError()
    {
        var (isValid, error) = DocumentReviewRequestValidator.ValidateRouteParameters(
            "", "doc-001", "trace-1");
        Assert.False(isValid);
        Assert.Equal("INVALID_REQUEST_BODY", error!.Code);
        Assert.Contains("projectId", error.Message);
    }

    [Fact]
    public void ValidateRouteParameters_EmptyDocumentId_ReturnsError()
    {
        var (isValid, error) = DocumentReviewRequestValidator.ValidateRouteParameters(
            "prj-001", "", "trace-1");
        Assert.False(isValid);
        Assert.Equal("INVALID_REQUEST_BODY", error!.Code);
        Assert.Contains("documentId", error.Message);
    }

    [Fact]
    public void ValidateRouteParameters_NullProjectId_ReturnsError()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateRouteParameters(null, Guid.NewGuid().ToString(), "trace-1");
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateRouteParameters_EmptyDocumentId_ReturnsError()
    {
        var (isValid, _) = DocumentReviewRequestValidator.ValidateRouteParameters(Guid.NewGuid().ToString(), "", "trace-1");
        Assert.False(isValid);
    }

    // Transition map coverage
    [Fact]
    public void TransitionMap_SubmitKeepsDraftStatus()
    {
        Assert.Equal("draft", DocumentReviewRequestValidator.TransitionMap["submit"].ResultStatus);
    }

    [Fact]
    public void TransitionMap_StartReviewResultsInInReview()
    {
        Assert.Equal("inReview", DocumentReviewRequestValidator.TransitionMap["startReview"].ResultStatus);
    }

    [Fact]
    public void TransitionMap_ApproveResultsInApproved()
    {
        Assert.Equal("approved", DocumentReviewRequestValidator.TransitionMap["approve"].ResultStatus);
    }

    [Fact]
    public void TransitionMap_RejectResultsInRejected()
    {
        Assert.Equal("rejected", DocumentReviewRequestValidator.TransitionMap["reject"].ResultStatus);
    }
}
