namespace OrderAgent.Domain;

/// <summary>
/// Violación de una regla de negocio. El mensaje es apto para mostrarse al cliente.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
