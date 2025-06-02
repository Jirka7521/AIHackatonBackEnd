namespace LLM
{
    public enum AttachmentType
    {
        Pdf,
        Image,
        Text
    }

    public enum AttachmentStatus
    {
        Uploaded,
        Processing,
        Ready
    }

    public class Attachment
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Uri? PreviewUrl { get; set; }
        public AttachmentType Type { get; set; }
        public AttachmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateAttachmentPayload
    {
        public string Name { get; set; }
        public AttachmentType Type { get; set; }
    }

    public class AttachmentNameAlreadyExistsError
    {
        public string Name { get; set; }
        public string Message => $"Attachment '{Name}' already exists";
    }
}
