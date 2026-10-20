using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_reconciliarcorreos</c> (diseno/03 1.2 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). TODO con SYSTEM: la cuenta de servicio que llama no se registra y
    /// no se crea ningún servicio de llamador (<see cref="ReconciliarCorreosApi"/> arma <paramref name="correos"/> y <paramref name="registrador"/> con el mismo servicio de SYSTEM, como S-34).
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana):
    /// </para>
    /// <list type="number">
    /// <item><c>Maximo</c> negativo ⇒ <c>CARP-REC-001</c>; después <c>MinutosMinimos</c> negativo ⇒ <c>CARP-REC-002</c>. Antes de cualquier lectura. Efectivos: <c>maximo</c> ausente o <c>0</c> ⇒ 20 y más de 100 ⇒ 100 (sin
    /// error); <c>minutos</c> ausente ⇒ 10 y <c>0</c> explícito ⇒ 0.</item>
    /// <item><c>bu = configuracion.BuId</c>: cualquier <c>CARP-CFG-*</c> aborta la llamada sin escrituras (PA-REC-13). Se lee SIEMPRE, también con <c>emailid</c>.</item>
    /// <item>CON <c>EmailId</c> (reintento dirigido; sin el filtro de antigüedad ni el tope): <c>correos.Leer(id, [directioncode, regardingobjectid, owningbusinessunit])</c>. (a) No existe, o <c>directioncode</c> no es explícitamente falso
    /// (verdadero o ausente: falla cerrado) ⇒ <c>CARP-REC-003</c> (PA-REC-10). (b) Su <c>owningbusinessunit</c> no es <c>bu</c>, o no viene ⇒ ítem <c>omitido</c> con <c>CARP-ENT-004</c>, SIN llamar al registrador (misma regla que 2.10 punto 13,
    /// evaluada acá primero para que el corte de (c) no esconda un correo de otra BU). (c) Su <c>regardingobjectid</c> es una referencia a <c>sanic_carp_tbl_conversacion</c> ⇒ ítem <c>yatenia</c> con esa conversación, SIN escrituras y SIN llamar al
    /// registrador (nota del orquestador en PA-REC-09: así una reconciliación dirigida de un correo ya contestado no vuelve a dejar pendiente la conversación, PA-ENT-04). No se lee la conversación: basta que el Regarding apunte a la tabla. (d) Si
    /// no, <c>registrador.Registrar(id)</c>. <c>haymas</c> es falso. <c>encontrados</c> = 1.</item>
    /// <item>SIN <c>EmailId</c>: <c>correos.EntrantesSinConversacion(bu, ahora - minutos, maximo + 1)</c> (ids por <c>createdon</c> ascendente). <c>ahora</c> = <c>reloj.AhoraUtc</c>, una lectura; si restar los minutos saldría de lo que
    /// <c>DateTime</c> admite se usa el 1753-01-01 UTC (el mínimo de Dataverse): <c>minutosminimos</c> enorme no es un error, solo no encuentra a nadie. <c>haymas</c> = más de <c>maximo</c> candidatos; se procesan los primeros <c>maximo</c>
    /// y <c>encontrados</c> es cuántos se procesaron. Cada candidato pasa por <c>registrador.Registrar(id)</c>.</item>
    /// <item>Cada resultado del registrador se anota en el <c>detalle</c> en orden: <c>Creada</c> → <c>creada</c>, <c>Unida</c> → <c>unida</c>, <c>YaTenia</c> → <c>yatenia</c> (los tres suman a <c>registrados</c>) y <c>Omitido(código)</c> →
    /// <c>omitido</c> con su <c>CARP-ENT-*</c> (suma a <c>conerror</c>); una omisión de negocio sigue con el siguiente candidato y NO lanza. Un fallo de la plataforma (K-01 de una carrera con S-34, datos, <c>CARP-CFG-*</c> de la
    /// configuración) NO se atrapa: la base lo vuelve <c>CARP-REC-900</c> y la transacción revierte el lote entero (PA-REC-15).</item>
    /// <item>Resultado: <c>Detalle</c> = <see cref="DetalleDeReconciliacion.ComoJson"/> de los ítems (<c>[]</c> sin candidatos). Es idempotente (un correo con conversación no es candidato) y la llamada no escribe nada si no hay candidatos (PA-REC-01).</item>
    /// </list>
    /// La traza lleva ids y códigos, nunca asunto ni remitente (PA-ENT-21). Sin estado mutable.
    /// </para>
    /// </summary>
    internal sealed class ReconciliarCorreosLogica
    {
        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> puede ser <c>null</c>). No consulta nada al construirse.</summary>
        public ReconciliarCorreosLogica(ICorreos correos, RegistradorDeCorreo registrador, IConfiguracionDelArea configuracion, IReloj reloj, Action<string> trazar)
        {
            if (correos == null) throw new ArgumentNullException(nameof(correos));
            if (registrador == null) throw new ArgumentNullException(nameof(registrador));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            if (reloj == null) throw new ArgumentNullException(nameof(reloj));
            _correos = correos;
            _registrador = registrador;
            _configuracion = configuracion;
            _reloj = reloj;
            _trazar = trazar;
        }

        private const int MaximoPorDefecto = 20;
        private const int MaximoAbsoluto = 100;
        private const int MinutosPorDefecto = 10;

        // Minimo de Dataverse para una fecha (decision 5 del lote 6).
        private static readonly DateTime FechaMinima = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly IReadOnlyCollection<string> ColumnasDelCorreoDirigido =
            new ReadOnlyCollection<string>(new[] { "directioncode", "regardingobjectid", "owningbusinessunit" });

        private readonly ICorreos _correos;
        private readonly RegistradorDeCorreo _registrador;
        private readonly IConfiguracionDelArea _configuracion;
        private readonly IReloj _reloj;
        private readonly Action<string> _trazar;

        /// <summary>Contrato en la descripción de la clase. <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeReconciliarCorreos Ejecutar(PedidoDeReconciliarCorreos pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: validar antes de cualquier lectura (PA-REC-07, PA6_REC_16).
            if (pedido.Maximo.HasValue && pedido.Maximo.Value < 0) throw CodigosDeError.REC_001.Crear();
            if (pedido.MinutosMinimos.HasValue && pedido.MinutosMinimos.Value < 0) throw CodigosDeError.REC_002.Crear();
            int maximo = !pedido.Maximo.HasValue || pedido.Maximo.Value == 0 ? MaximoPorDefecto : Math.Min(pedido.Maximo.Value, MaximoAbsoluto);
            int minutos = pedido.MinutosMinimos ?? MinutosPorDefecto;

            // Paso 2: la BU se lee siempre; un CARP-CFG-* aborta sin escrituras (PA-REC-13).
            Guid bu = _configuracion.BuId;

            var items = new List<ItemDeReconciliacion>();
            bool hayMas = false;

            if (pedido.EmailId.HasValue)
            {
                items.Add(Dirigido(pedido.EmailId.Value, bu));
            }
            else
            {
                var candidatos = _correos.EntrantesSinConversacion(bu, CorteDeAntiguedad(_reloj.AhoraUtc, minutos), maximo + 1);
                hayMas = candidatos.Count > maximo;
                // Una omision de negocio sigue; una falla de plataforma NO se atrapa: aborta el lote (PA-REC-15).
                for (int i = 0; i < candidatos.Count && i < maximo; i++)
                {
                    items.Add(Registrar(candidatos[i]));
                }
            }

            int conError = items.Count(i => i.Resultado == ResultadoDeReconciliacion.Omitido);
            Trazar("reconciliacion: " + items.Count.ToString(CultureInfo.InvariantCulture) + " procesados, " + conError.ToString(CultureInfo.InvariantCulture) + " omitidos");
            return new ResultadoDeReconciliarCorreos(items.Count, items.Count - conError, conError, hayMas, DetalleDeReconciliacion.ComoJson(items));
        }

        // Reintento dirigido (1.2 paso 2 y decision 2): sin antiguedad ni tope; se pregunta primero para no escribir ni atrapar nada.
        private ItemDeReconciliacion Dirigido(Guid emailId, Guid bu)
        {
            var correo = _correos.Leer(emailId, ColumnasDelCorreoDirigido);
            // Entrante es directioncode explicitamente falso: verdadero o ausente falla cerrado (PA-REC-10).
            if (correo == null) throw CodigosDeError.REC_003.Crear();
            var direccion = Valor(correo, "directioncode") as bool?;
            if (direccion != false) throw CodigosDeError.REC_003.Crear();

            // La BU va antes del corte de "yatenia" para que ese no esconda un correo de otra BU (decision 2).
            var unidad = Valor(correo, "owningbusinessunit") as ReferenciaDeEntidad;
            if (unidad == null || unidad.Id != bu)
            {
                var omision = CodigosDeError.ENT_004.Crear();
                Trazar("correo " + Texto(emailId) + ": omitido " + omision.Codigo);
                return DetalleDeReconciliacion.DeOmision(emailId, omision);
            }

            // Nota del orquestador en PA-REC-09: con Regarding a la tabla basta; no se lee la conversacion ni se llama al registrador.
            var regarding = Valor(correo, "regardingobjectid") as ReferenciaDeEntidad;
            if (regarding != null && string.Equals(regarding.Tabla, TablasDelArea.Conversacion, StringComparison.Ordinal))
            {
                Trazar("correo " + Texto(emailId) + ": yatenia " + Texto(regarding.Id));
                return new ItemDeReconciliacion(emailId, ResultadoDeReconciliacion.YaTenia, regarding.Id, null);
            }

            return Registrar(emailId);
        }

        private ItemDeReconciliacion Registrar(Guid emailId)
        {
            var resultado = _registrador.Registrar(emailId);
            switch (resultado.Tipo)
            {
                case TipoDeRegistro.Creada: return new ItemDeReconciliacion(emailId, ResultadoDeReconciliacion.Creada, resultado.ConversacionId, null);
                case TipoDeRegistro.Unida: return new ItemDeReconciliacion(emailId, ResultadoDeReconciliacion.Unida, resultado.ConversacionId, null);
                case TipoDeRegistro.YaTenia: return new ItemDeReconciliacion(emailId, ResultadoDeReconciliacion.YaTenia, resultado.ConversacionId, null);
                case TipoDeRegistro.Omitido: return DetalleDeReconciliacion.DeOmision(emailId, resultado.Omision);
                default: throw new InvalidOperationException("El registrador devolvió un tipo de resultado desconocido.");
            }
        }

        // ahora - minutos; si saldria del rango de DateTime o por debajo del minimo de Dataverse, se usa 1753-01-01 UTC (decision 5).
        private static DateTime CorteDeAntiguedad(DateTime ahora, int minutos)
        {
            long resta = (long)minutos * TimeSpan.TicksPerMinute;
            if (ahora.Ticks - resta < FechaMinima.Ticks) return FechaMinima;
            return ahora.AddTicks(-resta);
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
    }
}
