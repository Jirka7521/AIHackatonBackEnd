using Microsoft.AspNetCore.Mvc;

namespace LLM.Controllers
{
    [ApiController]
    [Route("chats")]
    public class ChatController : ControllerBase
    {
        private static readonly List<Chat> _chats = new();

        [HttpGet]
        public ActionResult<IEnumerable<Chat>> GetChats()
        {
            return Ok(_chats);
        }

        [HttpPost]
        public ActionResult<Chat> CreateChat([FromBody] CreateChatPayload payload)
        {
            if (_chats.Any(c => c.Name == payload.Name))
            {
                return Conflict(new ChatNameAlreadyExistsError { Name = payload.Name });
            }

            var chat = new Chat
            {
                Id = Guid.NewGuid().ToString(),
                Name = payload.Name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _chats.Add(chat);
            return Ok(chat);
        }

        [HttpGet("{id}")]
        public ActionResult<Chat> GetChat(string id)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == id);
            if (chat == null)
            {
                return NotFound();
            }

            return Ok(chat);
        }

        [HttpPut("{id}")]
        public ActionResult<Chat> UpdateChat(string id, [FromBody] UpdateChatPayload payload)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == id);
            if (chat == null)
            {
                return NotFound();
            }

            if (_chats.Any(c => c.Name == payload.Name && c.Id != id))
            {
                return Conflict(new ChatNameAlreadyExistsError { Name = payload.Name });
            }

            chat.Name = payload.Name;
            chat.UpdatedAt = DateTime.UtcNow;

            return Ok(chat);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteChat(string id)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == id);
            if (chat == null)
            {
                return NotFound();
            }

            _chats.Remove(chat);
            return NoContent();
        }
    }

    public class UpdateChatPayload
    {
        public string Name { get; set; }
    }
} 