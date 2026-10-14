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

        /// <summary>Identidades por defecto del área (Guid fijos distintos); los constructores de datos usan la del team propietario y la de la BU.</summary>
        public static readonly Guid IdDeBuPorDefecto = new Guid("dd000001-0000-4000-8000-000000000001");
        public static readonly Guid IdDeTeamPropietarioPorDefecto = new Guid("dd000001-0000-4000-8000-000000000002");
        public static readonly Guid IdDeTeamJefaturaPorDefecto = new Guid("dd000001-0000-4000-8000-000000000003");
        public static readonly Guid IdDeQueueRemitentePorDefecto = new Guid("dd000001-0000-4000-8000-000000000004");

        /// <summary>
        /// Las cuatro identidades con Guid fijos distintos y no vacíos, y <see cref="Variables"/> con las tres variables de 0.4 con un
        /// texto no vacío cada una.
        /// </summary>
        private readonly Dictionary<string, string> _variables = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Guid> _ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        private readonly Dictionary<string, ErrorDeNegocio> _fallas = new Dictionary<string, ErrorDeNegocio>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lecturas = new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly string[] Miembros = { nameof(BuId), nameof(TeamPropietarioId), nameof(TeamJefaturaId), nameof(QueueRemitenteId) };

        public ConfiguracionSimulada()
        {
            _ids[nameof(BuId)] = IdDeBuPorDefecto;
            _ids[nameof(TeamPropietarioId)] = IdDeTeamPropietarioPorDefecto;
            _ids[nameof(TeamJefaturaId)] = IdDeTeamJefaturaPorDefecto;
            _ids[nameof(QueueRemitenteId)] = IdDeQueueRemitentePorDefecto;
            foreach (var miembro in Miembros) _lecturas[miembro] = 0;
            _variables[VariableNombreBu] = "ARP PRUEBA";
            _variables[VariableNombreTeamJefatura] = "Jefatura PRUEBA";
            _variables[VariableBuzonArea] = "buzon.area@example.com";
        }

        /// <summary>Variables de entorno (nombre de esquema ⇒ valor). Se pueden agregar y quitar.</summary>
        public IDictionary<string, string> Variables { get { return _variables; } }

        /// <summary>
        /// Valor de una variable: lo que haya en <see cref="Variables"/>; si falta o es vacío, lanza el <see cref="ErrorDeNegocio"/>
        /// <c>CARP-CFG-006</c> con el nombre de la variable en el mensaje (0.4, punto 1), nunca otro valor.
        /// </summary>
        public string Valor(string variable)
        {
            string valor;
            if (variable == null) throw new ArgumentNullException(nameof(variable));
            if (!_variables.TryGetValue(variable, out valor) || string.IsNullOrEmpty(valor)) throw CodigosDeError.CFG_006.Crear(variable);
            return valor;
        }

        private Guid Leer(string miembro)
        {
            _lecturas[miembro]++;
            ErrorDeNegocio error;
            if (_fallas.TryGetValue(miembro, out error)) throw error;
            return _ids[miembro];
        }

        public Guid BuId { get { return Leer(nameof(BuId)); } set { _ids[nameof(BuId)] = value; } }
        public Guid TeamPropietarioId { get { return Leer(nameof(TeamPropietarioId)); } set { _ids[nameof(TeamPropietarioId)] = value; } }
        public Guid TeamJefaturaId { get { return Leer(nameof(TeamJefaturaId)); } set { _ids[nameof(TeamJefaturaId)] = value; } }
        public Guid QueueRemitenteId { get { return Leer(nameof(QueueRemitenteId)); } set { _ids[nameof(QueueRemitenteId)] = value; } }

        /// <summary>
        /// Hace que la lectura de <paramref name="miembro"/> (<c>nameof(BuId)</c>, <c>nameof(TeamPropietarioId)</c>,
        /// <c>nameof(TeamJefaturaId)</c> o <c>nameof(QueueRemitenteId)</c>) lance <paramref name="error"/> (la misma instancia). Otro
        /// nombre lanza <see cref="ArgumentException"/>. Un error nulo quita la falla.
        /// </summary>
        public void HacerFallar(string miembro, ErrorDeNegocio error)
        {
            ValidarMiembro(miembro);
            if (error == null) _fallas.Remove(miembro);
            else _fallas[miembro] = error;
        }

        private static void ValidarMiembro(string miembro)
        {
            if (Array.IndexOf(Miembros, miembro) < 0) throw new ArgumentException("Miembro desconocido: '" + miembro + "'.", nameof(miembro));
        }

        /// <summary>Cuántas veces se leyó cada miembro, por nombre (<c>nameof(BuId)</c>…). Un fallo también cuenta como lectura.</summary>
        public int Lecturas(string miembro)
        {
            ValidarMiembro(miembro);
            return _lecturas[miembro];
        }

        /// <summary>Suma de las lecturas de los cuatro miembros ("0 consultas de BU" ⇒ <c>LecturasTotales == 0</c>).</summary>
        public int LecturasTotales
        {
            get
            {
                var total = 0;
                foreach (var miembro in Miembros) total += _lecturas[miembro];
                return total;
            }
        }
    }
}
