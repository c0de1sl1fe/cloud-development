using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Messaging;

/// <summary>
/// Служба для отправки сгенерированных проектов в брокер сообщений.
/// </summary>
public interface IProjectPublisherService
{
    /// <summary>
    /// Отправляет проект в брокер сообщений.
    /// </summary>
    /// <param name="project">Программный проект</param>
    public Task SendMessage(SoftwareProject project);
}
