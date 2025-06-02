using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using LLM.Models;
using System.ComponentModel.DataAnnotations;

namespace LLM.Controllers
{
    [ApiController]
    [Route("api/chats")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet("workspace/{workspaceId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<Chat>>>> GetChatsByWorkspace(string workspaceId)
        {
            var chats = await _chatService.GetChatsByWorkspaceIdAsync(workspaceId);
            return Ok(ApiResponse<IEnumerable<Chat>>.SuccessResponse(chats));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<Chat>>> CreateChat([FromBody] CreateChatPayload payload)
        {
            var chat = await _chatService.CreateChatAsync(payload);
            return CreatedAtAction(nameof(GetChat), new { id = chat.Id }, 
                ApiResponse<Chat>.SuccessResponse(chat, "Chat created successfully"));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Chat>>> GetChat(string id)
        {
            var chat = await _chatService.GetChatByIdAsync(id);
            if (chat == null)
            {
                return NotFound(ApiResponse<Chat>.ErrorResponse("Chat not found"));
            }

            return Ok(ApiResponse<Chat>.SuccessResponse(chat));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<Chat>>> UpdateChat(string id, [FromBody] UpdateChatPayload payload)
        {
            var chat = await _chatService.UpdateChatAsync(id, payload.Name);
            if (chat == null)
            {
                return NotFound(ApiResponse<Chat>.ErrorResponse("Chat not found"));
            }

            return Ok(ApiResponse<Chat>.SuccessResponse(chat, "Chat updated successfully"));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteChat(string id)
        {
            var deleted = await _chatService.DeleteChatAsync(id);
            if (!deleted)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("Chat not found"));
            }

            return Ok(ApiResponse<object>.SuccessResponse(null, "Chat deleted successfully"));
        }
    }

    public class UpdateChatPayload
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }
} 