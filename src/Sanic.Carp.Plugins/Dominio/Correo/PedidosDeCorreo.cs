using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>
    /// Lo que <see cref="ReglasDeCorreo.EvaluarRemitente"/> necesita de UN <c>Create</c> de correo (S-31, diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). Sin <c>Entity</c>
    /// ni <c>EntityCollection</c>: el step convierte el <c>Target</c> a esto. Contenedor sin lógica.
    /// </summary>
    public sealed class PedidoDeRemitente
    {
        /// <summary><c>Target["directioncode"]</c> es verdadero (saliente). Una clave ausente o falsa es <c>false</c>: no se toca.</summary>
        public bool EsSaliente { get; }

        /// <summary><c>Target["regardingobjectid"]</c> convertido a neutro, o <c>null</c> si no viene.</summary>
        public ReferenciaDeEntidad Regarding { get; }

        /// <summary>
        /// Los <c>partyid</c> de <c>Target["from"]</c>, en el orden en que vienen, o <c>null</c> si <c>from</c> no viene en el <c>Target</c> (ausente). Una parte sin <c>partyid</c> (solo una
        /// dirección) el step la representa con <c>new ReferenciaDeEntidad(string.Empty, Guid.Empty)</c>: no es la queue ni el usuario, así que cuenta como "otra parte". Lista vacía = <c>from</c> vacío.
        /// </summary>
        public IReadOnlyList<ReferenciaDeEntidad> Remitentes { get; }

        /// <summary>Quién escribe. La regla solo lee <c>ActorId</c> (no consulta nada).</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La configuración del área; la regla pide <c>QueueRemitenteId</c> solo si el correo es saliente de una conversación (0 consultas en cualquier otro caso).</summary>
        public IConfiguracionDelArea Configuracion { get; }

        /// <summary><paramref name="regarding"/> y <paramref name="remitentes"/> pueden ser <c>null</c>; <paramref name="identidad"/> y <paramref name="configuracion"/> nulos ⇒ <see cref="ArgumentNullException"/>. No consulta nada.</summary>
        public PedidoDeRemitente(bool esSaliente, ReferenciaDeEntidad regarding, IReadOnlyList<ReferenciaDeEntidad> remitentes,
            IIdentidadDeEjecucion identidad, IConfiguracionDelArea configuracion)
        {
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            EsSaliente = esSaliente;
            Regarding = regarding;
            Remitentes = remitentes;
            Identidad = identidad;
            Configuracion = configuracion;
        }
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeCorreo.EvaluarRemitenteEnActualizacion"/> necesita de UN <c>Update</c> de correo (S-37, diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (cierre del sublote 5f)").
    /// Sin <c>Entity</c> ni <c>EntityCollection</c>. Contenedor sin lógica; no consulta nada.
    /// </summary>
    public sealed class PedidoDeRemitenteActualizado
    {
        /// <summary>
        /// El <c>Target</c> en neutro (<c>ContextoDeStep.TargetNeutro()</c>). La regla SOLO mira dos claves: <c>directioncode</c> (<c>bool</c>) y <c>regardingobjectid</c>
        /// (<see cref="ReferenciaDeEntidad"/>). Una clave presente con <c>null</c> es "vaciar esa columna" (DC-05); una ausente es "no se toca" y se completa con lo guardado. <c>from</c> NO va acá: va en <see cref="Remitentes"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>Los <c>partyid</c> de <c>Target["from"]</c> (igual que en <see cref="PedidoDeRemitente.Remitentes"/>), o <c>null</c> si el <c>Target</c> no trae <c>from</c>.</summary>
        public IReadOnlyList<ReferenciaDeEntidad> Remitentes { get; }

        /// <summary>El id del correo (<c>ContextoDeStep.IdPrimario</c>); lo usa <see cref="Lecturas"/>.</summary>
        public Guid EmailId { get; }

        /// <summary>Quién escribe. La regla solo lee <c>ActorId</c> (no consulta nada).</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La configuración del área; la regla pide <c>QueueRemitenteId</c> solo si el correo, ya con el <c>Update</c>, es saliente de una conversación.</summary>
        public IConfiguracionDelArea Configuracion { get; }

        /// <summary>La lectura con SYSTEM de lo guardado; la regla la pide como mucho UNA vez y solo si el <c>Target</c> no le alcanza.</summary>
        public ILecturasDelRemitente Lecturas { get; }

        /// <summary><paramref name="remitentes"/> puede ser <c>null</c>; <paramref name="target"/>, <paramref name="identidad"/>, <paramref name="configuracion"/> y <paramref name="lecturas"/> nulos ⇒ <see cref="ArgumentNullException"/>. No consulta nada.</summary>
        public PedidoDeRemitenteActualizado(IReadOnlyDictionary<string, object> target, IReadOnlyList<ReferenciaDeEntidad> remitentes, Guid emailId,
            IIdentidadDeEjecucion identidad, IConfiguracionDelArea configuracion, ILecturasDelRemitente lecturas)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            Target = target;
            Remitentes = remitentes;
            EmailId = emailId;
            Identidad = identidad;
            Configuracion = configuracion;
            Lecturas = lecturas;
        }
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeCorreo.EvaluarColumnasDelSistema"/> necesita de UN guardado de correo (S-32 y S-33, diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)").
    /// Contenedor sin lógica.
    /// </summary>
    public sealed class PedidoDeColumnasDelSistema
    {
        /// <summary>El <c>Target</c> en neutro (<c>ContextoDeStep.TargetNeutro()</c>). Una clave presente con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary><c>true</c> en S-32 (<c>Create</c>: no hay valor guardado), <c>false</c> en S-33 (<c>Update</c>).</summary>
        public bool EsAlta { get; }

        /// <summary>El id del correo (<c>ContextoDeStep.IdPrimario</c>); lo usa <see cref="Lecturas"/> en un <c>Update</c>. En un <c>Create</c> no se lee nada con él.</summary>
        public Guid EmailId { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido; la regla pregunta <c>EsEscrituraDelSistema</c> una sola vez y solo si ya vio algo que decidir.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del valor guardado; solo en un <c>Update</c> de una persona.</summary>
        public ILecturasDeCorreo Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>. No consulta nada.</summary>
        public PedidoDeColumnasDelSistema(IReadOnlyDictionary<string, object> target, bool esAlta, Guid emailId, IIdentidadDeEjecucion identidad, ILecturasDeCorreo lecturas)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            Target = target;
            EsAlta = esAlta;
            EmailId = emailId;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }
}
