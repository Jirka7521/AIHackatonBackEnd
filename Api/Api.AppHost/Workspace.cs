namespace LLM
{
    public class Workspace
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateWorkspacePayload
    {
        public string Name { get; set; }
    }

    public class WorkspaceNameAlreadyExistsError
    {
        public string Name { get; set; }
        public string Message => $"Workspace '{Name}' already exists";
    }
}