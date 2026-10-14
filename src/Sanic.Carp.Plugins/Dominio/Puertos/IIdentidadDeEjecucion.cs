using System;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Cómo un step distingue "sistema" de "persona" (diseno/03 0.3 y 4.2). Lo implementa <c>Infraestructura/IdentidadDeEjecucion</c>
    /// (lote 4) y lo sustituye <c>IdentidadSimulada</c> en las pruebas (4.5). Creado por el arquitecto en el lote 3 solo como
    /// interfaz, porque <c>IdentidadSimulada</c> lo necesita.
    /// </summary>
    public interface IIdentidadDeEjecucion
    {
        /// <summary><c>InitiatingUserId</c> del contexto, nunca <c>UserId</c> (0.3, V-4).</summary>
        Guid ActorId { get; }

        /// <summary><see cref="ActorId"/> es el usuario SYSTEM (0.3).</summary>
        bool EsSistema { get; }

        /// <summary>Igual que <see cref="EsSistema"/>; se conserva el nombre porque lo usan todos los steps (0.3).</summary>
        bool EsEscrituraDelSistema { get; }

        /// <summary><see cref="ActorId"/> figura en <c>teammembership</c> del team de jefatura. Consulta perezosa, una vez por ejecución (0.3).</summary>
        bool EsJefatura();
    }
}
