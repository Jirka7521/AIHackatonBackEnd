namespace Api.AppHost.Models;

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

public class WorkspaceNameAlreadyExistsError : Exception
{
    public string Name { get; set; }
    public string Message => $"Workspace '{Name}' already exists";
}