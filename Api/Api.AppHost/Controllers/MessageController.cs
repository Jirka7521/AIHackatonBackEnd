using Microsoft.AspNetCore.Mvc;

namespace LLM.Controllers
{
    [ApiController]
    [Route("chats/{chatId}/messages")]
    public class MessageController : ControllerBase
    {
        private static readonly Dictionary<string, List<Message>> _messagesByChat = new();

        [HttpGet]
        public ActionResult<IEnumerable<Message>> GetMessages(string chatId)
        {
            if (!_messagesByChat.ContainsKey(chatId))
            {
                _messagesByChat[chatId] = new List<Message>();
            }

            return Ok(_messagesByChat[chatId].OrderBy(m => m.CreatedAt));
        }

        [HttpPost]
        public ActionResult<Message> CreateMessage(string chatId, [FromBody] CreateMessagePayload payload)
        {
            if (!_messagesByChat.ContainsKey(chatId))
            {
                _messagesByChat[chatId] = new List<Message>();
            }

            var message = new Message
            {
                Id = payload.Id ?? Guid.NewGuid().ToString(),
                Content = payload.Content,
                Role = payload.Role,
                CreatedAt = payload.CreatedAt ?? DateTime.UtcNow
            };

            _messagesByChat[chatId].Add(message);
            return Ok(message);
        }

        [HttpGet("{id}")]
        public ActionResult<Message> GetMessage(string chatId, string id)
        {
            if (!_messagesByChat.ContainsKey(chatId))
            {
                return NotFound();
            }

            var message = _messagesByChat[chatId].FirstOrDefault(m => m.Id == id);
            if (message == null)
            {
                return NotFound();
            }

            return Ok(message);
        }

        [HttpPut("{id}")]
        public ActionResult<Message> UpdateMessage(string chatId, string id, [FromBody] UpdateMessagePayload payload)
        {
            if (!_messagesByChat.ContainsKey(chatId))
            {
                return NotFound();
            }

            var message = _messagesByChat[chatId].FirstOrDefault(m => m.Id == id);
            if (message == null)
            {
                return NotFound();
            }

            message.Content = payload.Content;
            return Ok(message);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteMessage(string chatId, string id)
        {
            if (!_messagesByChat.ContainsKey(chatId))
            {
                return NotFound();
            }

            var message = _messagesByChat[chatId].FirstOrDefault(m => m.Id == id);
            if (message == null)
            {
                return NotFound();
            }

            _messagesByChat[chatId].Remove(message);
            return NoContent();
        }

        [HttpDelete]
        public IActionResult ClearMessages(string chatId)
        {
            if (_messagesByChat.ContainsKey(chatId))
            {
                _messagesByChat[chatId].Clear();
            }

            return NoContent();
        }
    }

    public class UpdateMessagePayload
    {
        public string Content { get; set; }
    }
} 