using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LLM;

namespace LLM.Controllers
{
    [ApiController]
    [Route("attachments")]
    public class AttachmentController : ControllerBase
    {
        private static readonly List<Attachment> _attachments = new();

        [HttpGet]
        public ActionResult<IEnumerable<Attachment>> GetAttachments()
        {
            return Ok(_attachments);
        }

        [HttpPost]
        public ActionResult<Attachment> CreateAttachment([FromBody] CreateAttachmentPayload payload)
        {
            if (_attachments.Any(a => a.Name == payload.Name))
            {
                return Conflict(new AttachmentNameAlreadyExistsError { Name = payload.Name });
            }

            var attachment = new Attachment
            {
                Id = Guid.NewGuid().ToString(),
                Name = payload.Name,
                Type = payload.Type,
                Status = AttachmentStatus.Uploaded,
                CreatedAt = DateTime.UtcNow,
                PreviewUrl = null // Will be set when processing is complete
            };

            _attachments.Add(attachment);
            return Ok(attachment);
        }

        [HttpGet("{id}")]
        public ActionResult<Attachment> GetAttachment(string id)
        {
            var attachment = _attachments.FirstOrDefault(a => a.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            return Ok(attachment);
        }

        [HttpPut("{id}")]
        public ActionResult<Attachment> UpdateAttachment(string id, [FromBody] UpdateAttachmentPayload payload)
        {
            var attachment = _attachments.FirstOrDefault(a => a.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            if (_attachments.Any(a => a.Name == payload.Name && a.Id != id))
            {
                return Conflict(new AttachmentNameAlreadyExistsError { Name = payload.Name });
            }

            attachment.Name = payload.Name;
            if (payload.Status.HasValue)
            {
                attachment.Status = payload.Status.Value;
            }
            if (payload.PreviewUrl != null)
            {
                attachment.PreviewUrl = payload.PreviewUrl;
            }

            return Ok(attachment);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteAttachment(string id)
        {
            var attachment = _attachments.FirstOrDefault(a => a.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            _attachments.Remove(attachment);
            return NoContent();
        }

        [HttpPost("upload")]
        public async Task<ActionResult<Attachment>> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            if (_attachments.Any(a => a.Name == file.FileName))
            {
                return Conflict(new AttachmentNameAlreadyExistsError { Name = file.FileName });
            }

            // Determine attachment type based on file extension
            var attachmentType = GetAttachmentTypeFromFileName(file.FileName);

            var attachment = new Attachment
            {
                Id = Guid.NewGuid().ToString(),
                Name = file.FileName,
                Type = attachmentType,
                Status = AttachmentStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                PreviewUrl = null
            };

            _attachments.Add(attachment);

            // Simulate processing
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Simulate processing time
                attachment.Status = AttachmentStatus.Ready;
                attachment.PreviewUrl = new Uri($"https://example.com/previews/{attachment.Id}");
            });

            return Ok(attachment);
        }

        [HttpGet("{id}/download")]
        public IActionResult DownloadAttachment(string id)
        {
            var attachment = _attachments.FirstOrDefault(a => a.Id == id);
            if (attachment == null)
            {
                return NotFound();
            }

            // In a real implementation, you would return the actual file
            return Ok(new { message = $"Download {attachment.Name}", attachmentId = id });
        }

        private static AttachmentType GetAttachmentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => AttachmentType.Pdf,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => AttachmentType.Image,
                _ => AttachmentType.Text
            };
        }
    }

    public class UpdateAttachmentPayload
    {
        public string Name { get; set; } = string.Empty;
        public AttachmentStatus? Status { get; set; }
        public Uri? PreviewUrl { get; set; }
    }
}