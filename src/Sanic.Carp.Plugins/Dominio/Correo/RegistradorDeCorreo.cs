using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
            // Paso 1: se relee el Email (PA-ENT-22). Ninguna omision escribe ni consulta la configuracion (2.10 decision 3).
            var correo = _correos.Leer(emailId, ReglasDeEntradaDeCorreo.ColumnasDelCorreo);
            if (correo == null) return Omitir(emailId, CodigosDeError.ENT_002);
            // Entrante es directioncode explicitamente falso: verdadero o ausente falla cerrado (PA-ENT-13).
            var direccion = Valor(correo, "directioncode") as bool?;
            if (direccion != false) return Omitir(emailId, CodigosDeError.ENT_001);

            // Paso 2: la configuracion se resuelve antes de cualquier busqueda o escritura (PA-ENT-20, 2.10 decision 4).
            Guid teamId = _configuracion.TeamPropietarioId;

            DateTime llegada = ReglasDeEntradaDeCorreo.Llegada(Valor(correo, "actualend") as DateTime?, Valor(correo, "createdon") as DateTime?);

            // Paso 4: A, B o C por huella.
            var regarding = Valor(correo, "regardingobjectid") as ReferenciaDeEntidad;
            Guid? encontrada = null;
            string huella = null;
            if (regarding != null && string.Equals(regarding.Tabla, TablasDelArea.Conversacion, StringComparison.Ordinal))
            {
                encontrada = regarding.Id;
                Trazar("correo " + Texto(emailId) + ": ruta A");
            }
            else
            {
                // Un Regarding a otra tabla se ignora y solo se traza el nombre de la tabla (PA-ENT-19).
                if (regarding != null) Trazar("correo " + Texto(emailId) + ": el Regarding apunta a la tabla " + regarding.Tabla + " y se ignora");

                var variantes = ReglasDeEntradaDeCorreo.VariantesDeMessageId(Valor(correo, "inreplyto") as string);
                if (variantes.Count > 0)
                {
                    encontrada = _correos.BuscarPorMessageId(variantes);
                    if (encontrada.HasValue) Trazar("correo " + Texto(emailId) + ": ruta B");
                }

                if (!encontrada.HasValue)
                {
                    // Ruta C: se pregunta primero y se crea solo si no existe; asi se evita K-01 sin catch (2.10 decision 6).
                    huella = HuellaDeMessageId.Calcular(Valor(correo, "messageid") as string, emailId);
                    var existente = _conversaciones.BuscarPorHuella(huella, SinColumnas);
                    if (existente != null)
                    {
                        encontrada = existente.Id;
                        Trazar("correo " + Texto(emailId) + ": ruta C, huella " + huella + " ya existe");
                    }
                }
            }

            Guid conversacionId;
            bool creada = false;
            bool escribio = false;

            if (encontrada.HasValue)
            {
                // Paso 5 (DC-09): la Unida se redirige; si la cadena no termina, se omite sin escribir.
                var destino = _conversaciones.SeguirUnida(encontrada.Value, ReglasDeEntradaDeCorreo.SaltosDeUnion, ReglasDeEntradaDeCorreo.ColumnasDeLaConversacion);
                if (destino == null) return Omitir(emailId, CodigosDeError.ENT_003);
                conversacionId = destino.Id;

                // Paso 7: solo se escribe la conversacion si la llegada cambia algo (PA-ENT-05).
                var cambios = ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDe(destino), Valor(destino, "sanic_sinrespuestadesde") as DateTime?,
                    Valor(destino, "sanic_correonuevoenfinalizada") as bool? == true, llegada);
                if (cambios.Count > 0)
                {
                    _conversaciones.Actualizar(conversacionId, cambios, null);
                    escribio = true;
                }
            }
            else
            {
                // Paso 6: el cliente se consulta solo con remitente (PA-ENT-15) y solo cuenta si hay exactamente uno (DC-14).
                string remitente = ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(Valor(correo, "sender") as string);
                Guid? clienteId = null;
                if (remitente != null)
                {
                    var clientes = _clientes.BuscarActivosPorCorreo(remitente, 2);
                    if (clientes.Count == 1) clienteId = clientes[0];
                }

                var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(huella, llegada,
                    ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(Valor(correo, "subject") as string), remitente, teamId, clienteId);
                conversacionId = _conversaciones.Crear(columnas);
                creada = true;
                Trazar("correo " + Texto(emailId) + ": conversacion " + Texto(conversacionId) + " creada con huella " + huella);
            }

            // Paso 8: el Email va despues de la conversacion (2.10 decision 9).
            if (regarding == null || regarding.Id != conversacionId || !string.Equals(regarding.Tabla, TablasDelArea.Conversacion, StringComparison.Ordinal))
            {
                _correos.ApuntarA(emailId, conversacionId);
                escribio = true;
            }
            var propietario = Valor(correo, "ownerid") as ReferenciaDeEntidad;
            if (propietario == null || propietario.Id != teamId)
            {
                _correos.AsignarAlTeam(emailId, teamId);
                escribio = true;
            }

            ResultadoDeRegistro resultado = creada ? ResultadoDeRegistro.Creada(conversacionId)
                : (escribio ? ResultadoDeRegistro.Unida(conversacionId) : ResultadoDeRegistro.YaTenia(conversacionId));
            Trazar("correo " + Texto(emailId) + ": " + resultado.Tipo + " en " + Texto(conversacionId));
            return resultado;
        }

        private static readonly IReadOnlyCollection<string> SinColumnas = new ReadOnlyCollection<string>(new string[0]);

        private ResultadoDeRegistro Omitir(Guid emailId, DefinicionDeError definicion)
        {
            var motivo = definicion.Crear();
            Trazar("correo " + Texto(emailId) + ": omitido " + motivo.Codigo);
            return ResultadoDeRegistro.Omitido(motivo);
        }

        private void Trazar(string texto)
        {
            if (_trazar != null) _trazar(texto);
        }

        private static string Texto(Guid id)
        {
            return id.ToString("D", CultureInfo.InvariantCulture);
        }

        private static object Valor(FilaDeDatos fila, string columna)
        {
            object valor;
            return fila.Valores.TryGetValue(columna, out valor) ? valor : null;
        }

        // Un par statecode/statuscode que 0.7 no conoce es un dato corrupto, no un caso de negocio.
        private static EstadoDeConversacion EstadoDe(FilaDeDatos conversacion)
        {
            var estado = Valor(conversacion, "statecode") as ValorDeOpcion;
            var motivo = Valor(conversacion, "statuscode") as ValorDeOpcion;
            if (estado == null || motivo == null) throw new InvalidOperationException("La conversacion no trae statecode y statuscode.");
            var conocido = Estados.DeConversacion(new EstadoEfectivo(estado.Valor, motivo.Valor));
            if (!conocido.HasValue) throw new InvalidOperationException("La conversacion trae un par statecode/statuscode que no existe (diseno/03 0.7).");
            return conocido.Value;
        }
    }
}
