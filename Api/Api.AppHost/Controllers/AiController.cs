using Microsoft.AspNetCore.Mvc;
using LLM.Models;
using LLM.Services;

namespace LLM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and process a PDF file for vector storage
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<OperationResult>>> UploadFile([FromBody] UploadFileRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return BadRequest(ApiResponse<OperationResult>.ErrorResponse("File path is required"));
            }

            var result = await _aiService.UploadFileAsync(request.FilePath);
            
            if (result.Status == OperationStatus.Success)
            {
                return Ok(ApiResponse<OperationResult>.SuccessResponse(result));
            }
            
            return BadRequest(ApiResponse<OperationResult>.ErrorResponse(result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FilePath}", request.FilePath);
            return StatusCode(500, ApiResponse<OperationResult>.ErrorResponse("Internal server error"));
        }
    }

    /// <summary>
    /// Query similar vectors based on text input
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<ApiResponse<List<QueryResult>>>> QueryVectors([FromBody] QueryVectorsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(ApiResponse<List<QueryResult>>.ErrorResponse("Query is required"));
            }

            if (request.Count <= 0)
            {
                return BadRequest(ApiResponse<List<QueryResult>>.ErrorResponse("Count must be greater than zero"));
            }

            var results = await _aiService.QueryVectorsAsync(request.Query, request.Count);
            return Ok(ApiResponse<List<QueryResult>>.SuccessResponse(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying vectors: {Query}", request.Query);
            return StatusCode(500, ApiResponse<List<QueryResult>>.ErrorResponse("Internal server error"));
        }
    }

    /// <summary>
    /// Get file path for a specific vector ID
    /// </summary>
    [HttpGet("filepath/{id}")]
    public async Task<ActionResult<ApiResponse<string>>> GetFilePath(int id)
    {
        try
        {
            var filePath = await _aiService.GetFilePathAsync(id);
            return Ok(ApiResponse<string>.SuccessResponse(filePath));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<string>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file path for ID: {Id}", id);
            return StatusCode(500, ApiResponse<string>.ErrorResponse("Internal server error"));
        }
    }

    /// <summary>
    /// Chat with AI using RAG (Retrieval Augmented Generation)
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> Chat([FromBody] AiChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<AiChatResponse>.ErrorResponse("Message is required"));
            }

            var response = await _aiService.ChatAsync(request);
            return Ok(ApiResponse<AiChatResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI chat: {Message}", request.Message);
            return StatusCode(500, ApiResponse<AiChatResponse>.ErrorResponse("Internal server error"));
        }
    }
}

public class UploadFileRequest
{
    public string FilePath { get; set; } = string.Empty;
}

public class QueryVectorsRequest
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
} 