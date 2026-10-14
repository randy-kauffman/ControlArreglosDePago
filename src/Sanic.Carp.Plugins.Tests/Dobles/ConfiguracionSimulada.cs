using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IConfiguracionDelArea"/> sembrada (diseno/03 4.5): variables de entorno en un diccionario; BU, teams y queue ya
    /// resueltos; contadores de lecturas. No memoriza: cada lectura de un miembro cuenta. Una lectura puede fallar con el
    /// <see cref="ErrorDeNegocio"/> <c>CARP-CFG-*</c> que se le pida, como la implementación real (0.4).
    /// </summary>
    internal sealed class ConfiguracionSimulada : IConfiguracionDelArea
    {
        /// <summary>Nombres de las variables de entorno que lee el área (0.4).</summary>
        public const string VariableNombreBu = "sanic_carp_ev_nombrebu";
        public const string VariableNombreTeamJefatura = "sanic_carp_ev_nombreteamjefatura";
        public const string VariableBuzonArea = "sanic_carp_ev_buzonarea";

        /// <summary>
        /// Las cuatro identidades con Guid fijos distintos y no vacíos, y <see cref="Variables"/> con las tres variables de 0.4 con un
        /// texto no vacío cada una.
        /// </summary>
        public ConfiguracionSimulada() { throw new NotImplementedException(); }

        /// <summary>Variables de entorno (nombre de esquema ⇒ valor). Se pueden agregar y quitar.</summary>
        public IDictionary<string, string> Variables { get { throw new NotImplementedException(); } }

        /// <summary>
        /// Valor de una variable: lo que haya en <see cref="Variables"/>; si falta o es vacío, lanza el <see cref="ErrorDeNegocio"/>
        /// <c>CARP-CFG-006</c> con el nombre de la variable en el mensaje (0.4, punto 1), nunca otro valor.
        /// </summary>
        public string Valor(string variable) { throw new NotImplementedException(); }

        public Guid BuId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public Guid TeamPropietarioId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public Guid TeamJefaturaId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public Guid QueueRemitenteId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>
        /// Hace que la lectura de <paramref name="miembro"/> (<c>nameof(BuId)</c>, <c>nameof(TeamPropietarioId)</c>,
        /// <c>nameof(TeamJefaturaId)</c> o <c>nameof(QueueRemitenteId)</c>) lance <paramref name="error"/> (la misma instancia). Otro
        /// nombre lanza <see cref="ArgumentException"/>. Un error nulo quita la falla.
        /// </summary>
        public void HacerFallar(string miembro, ErrorDeNegocio error) { throw new NotImplementedException(); }

        /// <summary>Cuántas veces se leyó cada miembro, por nombre (<c>nameof(BuId)</c>…). Un fallo también cuenta como lectura.</summary>
        public int Lecturas(string miembro) { throw new NotImplementedException(); }

        /// <summary>Suma de las lecturas de los cuatro miembros ("0 consultas de BU" ⇒ <c>LecturasTotales == 0</c>).</summary>
        public int LecturasTotales { get { throw new NotImplementedException(); } }
    }
}
