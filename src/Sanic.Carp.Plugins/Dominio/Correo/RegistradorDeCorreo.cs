using System;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>Cómo terminó <see cref="RegistradorDeCorreo.Registrar"/> (2.10 paso 8).</summary>
    public enum TipoDeRegistro
    {
        /// <summary>La ruta C creó una conversación nueva.</summary>
        Creada,

        /// <summary>El correo quedó en una conversación que ya existía (rutas A, B o C encontrada por huella) y hubo AL MENOS una escritura (conversación, Regarding o propietario del Email).</summary>
        Unida,

        /// <summary>El correo ya estaba en la conversación correcta y no hubo ninguna escritura.</summary>
        YaTenia,

        /// <summary>No se registró: <see cref="ResultadoDeRegistro.Omision"/> dice por qué (<c>CARP-ENT-001</c>, <c>-002</c> o <c>-003</c>). No es una excepción.</summary>
        Omitido
    }

    /// <summary>El resultado de registrar UN correo. Inmutable; se arma con las cuatro fábricas.</summary>
    public sealed class ResultadoDeRegistro
    {
        /// <summary>Cómo terminó.</summary>
        public TipoDeRegistro Tipo { get; }

        /// <summary>La conversación final (la creada, o la destino tras seguir las Unidas). <c>null</c> si <see cref="Tipo"/> es <see cref="TipoDeRegistro.Omitido"/>.</summary>
        public Guid? ConversacionId { get; }

        /// <summary>El motivo de la omisión (<c>CARP-ENT-001</c>, <c>-002</c> o <c>-003</c>); <c>null</c> si no se omitió.</summary>
        public ErrorDeNegocio Omision { get; }

        private ResultadoDeRegistro(TipoDeRegistro tipo, Guid? conversacionId, ErrorDeNegocio omision)
        {
            Tipo = tipo;
            ConversacionId = conversacionId;
            Omision = omision;
        }

        /// <summary>Resultado <see cref="TipoDeRegistro.Creada"/>.</summary>
        public static ResultadoDeRegistro Creada(Guid conversacionId)
        {
            return new ResultadoDeRegistro(TipoDeRegistro.Creada, conversacionId, null);
        }

        /// <summary>Resultado <see cref="TipoDeRegistro.Unida"/>.</summary>
        public static ResultadoDeRegistro Unida(Guid conversacionId)
        {
            return new ResultadoDeRegistro(TipoDeRegistro.Unida, conversacionId, null);
        }

        /// <summary>Resultado <see cref="TipoDeRegistro.YaTenia"/>.</summary>
        public static ResultadoDeRegistro YaTenia(Guid conversacionId)
        {
            return new ResultadoDeRegistro(TipoDeRegistro.YaTenia, conversacionId, null);
        }

        /// <summary>Resultado <see cref="TipoDeRegistro.Omitido"/>. <paramref name="motivo"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static ResultadoDeRegistro Omitido(ErrorDeNegocio motivo)
        {
            if (motivo == null) throw new ArgumentNullException(nameof(motivo));
            return new ResultadoDeRegistro(TipoDeRegistro.Omitido, null, motivo);
        }
    }

    /// <summary>
    /// <c>RegistradorDeCorreo</c> (diseno/03 2.10 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"): pone UN correo entrante en su conversación. Lo llaman S-34
    /// (<c>RegistrarCorreoEntranteStep</c>) y, más adelante, la Custom API <c>reconciliarcorreos</c> (1.2). Vive en <c>Dominio/Correo</c> porque solo conoce puertos (no el SDK); es
    /// <c>internal</c> porque los puertos de datos lo son. TODO el trabajo va por los puertos que recibe: el que arma la clase decide con qué servicio los construyó (S-34 y la API,
    /// siempre SYSTEM, PA-ENT-23).
    /// </summary>
    internal sealed class RegistradorDeCorreo
    {
        private readonly ICorreos _correos;
        private readonly IConversaciones _conversaciones;
        private readonly IClientes _clientes;
        private readonly IConfiguracionDelArea _configuracion;
        private readonly Action<string> _trazar;

        /// <summary>
        /// Los cuatro puertos son obligatorios (nulo ⇒ <see cref="ArgumentNullException"/>). <paramref name="trazar"/> puede ser <c>null</c> (no traza). Solo recibe ids, códigos y nombres de
        /// columna: NUNCA asunto, remitente ni cuerpo (PA-ENT-21). No consulta nada al construirse.
        /// </summary>
        public RegistradorDeCorreo(ICorreos correos, IConversaciones conversaciones, IClientes clientes, IConfiguracionDelArea configuracion, Action<string> trazar)
        {
            if (correos == null) throw new ArgumentNullException(nameof(correos));
            if (conversaciones == null) throw new ArgumentNullException(nameof(conversaciones));
            if (clientes == null) throw new ArgumentNullException(nameof(clientes));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            _correos = correos;
            _conversaciones = conversaciones;
            _clientes = clientes;
            _configuracion = configuracion;
            _trazar = trazar;
        }

        /// <summary>
        /// Registra el correo <paramref name="emailId"/> (2.10 pasos 1 a 8), en este orden y con estas consultas y escrituras. NO atrapa ninguna falla de datos (0.5): una K-01 de la
        /// creación, o cualquier <c>FaultException</c>, sube tal cual (el job asíncrono falla y la reconciliación lo repara).
        /// <list type="number">
        /// <item><b>Releer</b> el Email con <c>ICorreos.Leer(emailId, ReglasDeEntradaDeCorreo.ColumnasDelCorreo)</c> (no se confía en el <c>Target</c>, PA-ENT-22). No existe ⇒
        /// <c>Omitido(CARP-ENT-002)</c>. Solo es entrante si <c>directioncode</c> es explícitamente <c>false</c>; verdadero o ausente ⇒ <c>Omitido(CARP-ENT-001)</c> (falla cerrado, PA-ENT-13).
        /// Ninguna escritura en las dos omisiones, y la configuración NO se consulta antes de ellas.</item>
        /// <item><b>Configuración</b>: <c>Configuracion.TeamPropietarioId</c> se lee AQUÍ, antes de cualquier escritura, de modo que un <c>CARP-CFG-001</c>, <c>-002</c> o <c>-006</c>
        /// sube sin haber escrito nada (PA-ENT-20). Se lee una sola vez.</item>
        /// <item>Llegada = <c>ReglasDeEntradaDeCorreo.Llegada(actualend, createdon)</c>.</item>
        /// <item><b>Determinar la conversación</b>. <b>A</b>: el Regarding es una <c>ReferenciaDeEntidad</c> de <c>sanic_carp_tbl_conversacion</c> ⇒ esa conversación. <b>B</b> (DC-08): sin Regarding
        /// o con uno que no es conversación (se ignora y se traza el nombre de la tabla, PA-ENT-19) y con variantes de <c>inreplyto</c> (<c>ReglasDeEntradaDeCorreo.VariantesDeMessageId</c>) ⇒
        /// <c>ICorreos.BuscarPorMessageId(variantes)</c>; si devuelve una conversación, esa. <b>C</b>: si A y B no dieron nada, huella = <c>HuellaDeMessageId.Calcular(messageid, emailId)</c> y
        /// <c>IConversaciones.BuscarPorHuella(huella, columnas vacías)</c> (K-01, cualquier estado); si existe, esa (idempotencia, D-03, PA-ENT-03); si no, hay que crear (paso 6).</item>
        /// <item><b>Unida</b> (DC-09): la conversación encontrada (A, B o C) se pasa por <c>IConversaciones.SeguirUnida(id, ReglasDeEntradaDeCorreo.SaltosDeUnion, ColumnasDeLaConversacion)</c>;
        /// devuelve la propia fila si no está Unida. <c>null</c> ⇒ <c>Omitido(CARP-ENT-003)</c> sin escribir nada (PA-ENT-09 y PA-ENT-10).</item>
        /// <item><b>Crear</b> (ruta C sin huella existente): cliente = el que devuelve <c>IClientes.BuscarActivosPorCorreo(remitente normalizado, 2)</c> SOLO si es exactamente uno y el remitente
        /// no es nulo (si lo es, no se consulta, PA-ENT-15); <c>IConversaciones.Crear(ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(...))</c>. La creación NO atrapa K-01.</item>
        /// <item><b>Actualizar la existente</b> (A, B o C por huella): del destino leído en el paso 5, <c>ReglasDeEntradaDeCorreo.AplicarLlegada(estado, sinrespuestadesde, indicador, llegada)</c>
        /// con el estado de <c>Estados.DeConversacion(new EstadoEfectivo(statecode, statuscode))</c> (un par que no existe ⇒ <see cref="InvalidOperationException"/>); si devuelve columnas,
        /// <c>IConversaciones.Actualizar(id, columnas, null)</c> (sin versión de fila); si no, NO se escribe (PA-ENT-05). Esta escritura va ANTES que las del Email (paso 8): si la siguiente falla,
        /// el correo queda sin conversación y la reconciliación lo recoge.</item>
        /// <item><b>Email</b>: si su Regarding no es la conversación final (ni siquiera del tipo correcto), <c>ICorreos.ApuntarA(emailId, conversacionId)</c>; DESPUÉS, si su <c>ownerid</c> no es
        /// <c>TeamPropietarioId</c> (compara el id), <c>ICorreos.AsignarAlTeam(emailId, team)</c> (PA-ENT-14: si ya es el team, no hay <c>Assign</c>).</item>
        /// <item><b>Resultado</b>: <see cref="TipoDeRegistro.Creada"/> si el paso 6 creó; si no, <see cref="TipoDeRegistro.YaTenia"/> cuando NINGUNA escritura se hizo en los pasos 7 y 8, y
        /// <see cref="TipoDeRegistro.Unida"/> si hubo alguna (PA-ENT-02, PA-ENT-03, PA-ENT-04).</item>
        /// </list>
        /// Todo con los servicios con que se armaron los puertos (SYSTEM en producción). La traza lleva ids, el nombre de la ruta y el código de una omisión; nunca asunto, remitente ni
        /// cuerpo.
        /// </summary>
        public ResultadoDeRegistro Registrar(Guid emailId)
        {
            throw new NotImplementedException();
        }
    }
}
