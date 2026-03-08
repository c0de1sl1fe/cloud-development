namespace ProjectApp.Domain.Entities;

/// <summary>
/// Программный проект
/// </summary>
public class SoftwareProject
{
    /// <summary>
    /// Идентификатор в системе
    /// </summary>
    public required int Id { get; set; }
    /// <summary>
    /// Название проекта
    /// </summary>
    public required string ProjectName { get; set; }
    /// <summary>
    /// Заказчик проекта
    /// </summary>
    public required string Customer { get; set; }
    /// <summary>
    /// Менеджер проекта
    /// </summary>
    public required string ProjectManager { get; set; }
    /// <summary>
    /// Дата начала
    /// </summary>
    public DateOnly StartDate { get; set; }
    /// <summary>
    /// Плановая дата завершения
    /// </summary>
    public DateOnly PlannedEndDate { get; set; }
    /// <summary>
    /// Фактическая дата завершения
    /// </summary>
    public DateOnly? ActualEndDate { get; set; }
    /// <summary>
    /// Бюджет
    /// </summary>
    public decimal Budget { get; set; }
    /// <summary>
    /// Фактические затраты
    /// </summary>
    public decimal ActualCost { get; set; }
    /// <summary>
    /// Процент выполнения
    /// </summary>
    public int CompletionPercentage { get; set; }
}
