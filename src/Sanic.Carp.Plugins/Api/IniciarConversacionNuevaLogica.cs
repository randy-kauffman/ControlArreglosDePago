using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_iniciarconversacionnueva</c> (diseno/03 1.5 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Valida el acceso con el LLAMADOR y escribe como SYSTEM. Dos servicios por ejecución:
    /// LLAMADOR para <paramref name="conversacionesComoLlamador"/> (solo el paso 1); SYSTEM para <paramref name="conversacionesComoSistema"/>, <paramref name="correos"/>, la configuración y todas las escrituras.
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana; ninguna escritura antes del paso 5):
    /// </para>
    /// <list type="number">
    /// <item><c>ConversacionId</c> nulo ⇒ <c>CARP-INI-001</c>. Con el llamador, <c>Leer(id, [statecode, statuscode, sanic_correonuevoenfinalizada, sanic_fechafinalizacion, sanic_clienteid])</c>; <c>null</c> (no existe O sin acceso) ⇒ <c>CARP-INI-002</c>, el mismo texto.</item>
    /// <item>Debe estar Finalizada (<c>Estados.De(EstadoDeConversacion.Finalizada)</c>, 1/2) Y con <c>sanic_correonuevoenfinalizada</c> verdadero; Abierta, Unida, Finalizada sin indicador o una combinación desconocida ⇒ <c>CARP-INI-003</c>.</item>
    /// <item>Con SYSTEM: <c>correos.EntrantesPosterioresA(id, sanic_fechafinalizacion, [messageid, subject, sender], 1001)</c> (entrantes con llegada = <c>actualend</c> y, si es nula, <c>createdon</c>, estrictamente posterior; DC-17; el puerto ordena por llegada ascendente y su orden vale
    /// porque el tope se aplica antes). Una Finalizada SIN <c>sanic_fechafinalizacion</c> es un dato corrupto: ⇒ <c>CARP-INI-004</c> (el diseño no lo dice; decisión del arquitecto, misma familia "indicador inconsistente, avise a soporte"). Ninguno ⇒ <c>CARP-INI-004</c>
    /// (el indicador no se limpia solo). Más de 1.000 (el puerto devuelve 1.001) ⇒ <c>CARP-INI-006</c>.</item>
    /// <item>El primero, <c>E1</c>: huella = <c>HuellaDeMessageId.Calcular(messageid, E1.Id)</c> (sin <c>messageid</c> usa el respaldo <c>sin-messageid:&lt;id&gt;</c>, DC-13, PA-INI-08). <c>conversacionesComoSistema.BuscarPorHuella(huella, [])</c> distinto de <c>null</c> (en CUALQUIER estado) ⇒
    /// <c>CARP-INI-005</c> (PA-INI-04). Así se evita K-01; la ventana entre preguntar y crear la cubre la clave y la red es el reintento (<c>CARP-INI-900</c>).</item>
    /// <item><c>configuracion.TeamPropietarioId</c> (una lectura; un <c>CARP-CFG-*</c> sube sin haber escrito nada). Después, con SYSTEM y en este orden, sin atrapar nada: (a) <c>conversacionesComoSistema.Crear(ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(huella, llegada(E1),
    /// AsuntoDeLaConversacion(E1.subject), RemitenteDeLaConversacion(E1.sender), team, cliente de la anterior))</c>: SIN oficial, SIN <c>sanic_nombre</c> ni estado (los completa la plataforma; <c>ConversacionCrearStep</c> deja pasar el alta del sistema, D-41) y con
    /// <c>sanic_clienteid</c> solo si la anterior lo tiene (PA-INI-10); (b) por cada correo de la lista: <c>correos.ApuntarA(id, nueva)</c> (solo <c>regardingobjectid</c>); (c) la anterior: <c>conversacionesComoSistema.Actualizar(id, {sanic_correonuevoenfinalizada = false,
    /// sanic_sinrespuestadesde = null}, null)</c>, que sigue Finalizada (V-2: la plataforma deja escribir en una fila inactiva; el step la deja pasar por ser del sistema, DC-03). Los correos que no son entrantes posteriores (<c>e1</c>, <c>s1</c> de PA-INI-01) se quedan.</item>
    /// <item>Resultado: <c>NuevaConversacionId</c> = el id del paso 5a, <c>CorreosMovidos</c> = cuántos correos se movieron.</item>
    /// </list>
    /// Es UNA transacción: una carrera con la misma huella (K-01 <c>0x80060892</c>) y cualquier otra falla de la plataforma NO se atrapan; la base los vuelve <c>CARP-INI-900</c> y revierte (PA-INI-09, PA-INI-15); un reintento ve <c>CARP-INI-003</c> o <c>-005</c>. Traza solo ids y códigos
    /// (ni asunto ni remitente ni la huella, que deriva del Message-ID; PA-ENT-25).
    /// </para>
    /// </summary>
    internal sealed class IniciarConversacionNuevaLogica
    {
        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> puede ser <c>null</c>). No consulta nada al construirse.</summary>
        public IniciarConversacionNuevaLogica(IConversaciones conversacionesComoLlamador, IConversaciones conversacionesComoSistema, ICorreos correos, IConfiguracionDelArea configuracion, Action<string> trazar)
        {
            if (conversacionesComoLlamador == null) throw new ArgumentNullException(nameof(conversacionesComoLlamador));
            if (conversacionesComoSistema == null) throw new ArgumentNullException(nameof(conversacionesComoSistema));
            if (correos == null) throw new ArgumentNullException(nameof(correos));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            _comoLlamador = conversacionesComoLlamador;
            _comoSistema = conversacionesComoSistema;
            _correos = correos;
            _configuracion = configuracion;
            _trazar = trazar;
        }

        // Mas de 1.000 correos no se mueven en una operacion (DC-16); se pide uno de mas para detectarlo.
        private const int TopeDeCorreos = 1000;

        private static readonly IReadOnlyCollection<string> ColumnasDeLaConversacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_correonuevoenfinalizada", "sanic_fechafinalizacion", "sanic_clienteid"
        });

        private static readonly IReadOnlyCollection<string> ColumnasDelCorreo = new ReadOnlyCollection<string>(new[] { "messageid", "subject", "sender" });
        private static readonly IReadOnlyCollection<string> SinColumnas = new ReadOnlyCollection<string>(new string[0]);

        private readonly IConversaciones _comoLlamador;
        private readonly IConversaciones _comoSistema;
        private readonly ICorreos _correos;
        private readonly IConfiguracionDelArea _configuracion;
        private readonly Action<string> _trazar;

        /// <summary>Contrato en la descripción de la clase. <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeIniciarConversacionNueva Ejecutar(PedidoDeIniciarConversacionNueva pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: -001 y lectura con el LLAMADOR; no existe y sin acceso dan el mismo -002 para no revelar existencia.
            if (!pedido.ConversacionId.HasValue) throw CodigosDeError.INI_001.Crear();
            Guid anteriorId = pedido.ConversacionId.Value;
            var anterior = _comoLlamador.Leer(anteriorId, ColumnasDeLaConversacion);
            if (anterior == null) throw CodigosDeError.INI_002.Crear();

            // Paso 2: Finalizada (1/2) Y con el indicador; cualquier otra combinacion, incluida una desconocida, es -003.
            var estado = new EstadoEfectivo(Opcion(anterior, "statecode"), Opcion(anterior, "statuscode"));
            var indicador = Valor(anterior, "sanic_correonuevoenfinalizada") as bool?;
            if (!estado.Equals(Estados.De(EstadoDeConversacion.Finalizada)) || indicador != true)
            {
                throw CodigosDeError.INI_003.Crear();
            }

            // Paso 3: sin fecha de finalizacion es un dato corrupto (decision 2 del lote 6); sin entrantes posteriores, el indicador esta inconsistente.
            var finalizacion = Valor(anterior, "sanic_fechafinalizacion") as DateTime?;
            if (!finalizacion.HasValue) throw CodigosDeError.INI_004.Crear();
            var correos = _correos.EntrantesPosterioresA(anteriorId, finalizacion.Value, ColumnasDelCorreo, TopeDeCorreos + 1);
            if (correos.Count == 0) throw CodigosDeError.INI_004.Crear();
            if (correos.Count > TopeDeCorreos) throw CodigosDeError.INI_006.Crear();

            // Paso 4: se pregunta antes de crear (en cualquier estado) para no depender de atrapar K-01 (0.5).
            var primero = correos[0];
            string huella = HuellaDeMessageId.Calcular(Valor(primero, "messageid") as string, primero.Id);
            if (_comoSistema.BuscarPorHuella(huella, SinColumnas) != null) throw CodigosDeError.INI_005.Crear();

            // Paso 5: la configuracion se lee antes de la primera escritura, asi un CARP-CFG-* sube sin haber escrito nada.
            Guid team = _configuracion.TeamPropietarioId;
            DateTime llegada = ReglasDeEntradaDeCorreo.Llegada(Valor(primero, "actualend") as DateTime?, Valor(primero, "createdon") as DateTime?);
            var cliente = Valor(anterior, "sanic_clienteid") as ReferenciaDeEntidad;
            var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(huella, llegada,
                ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(Valor(primero, "subject") as string),
                ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(Valor(primero, "sender") as string),
                team, cliente == null ? (Guid?)null : cliente.Id);

            // Las tres escrituras van con SYSTEM, en este orden y sin atrapar nada: la base las vuelve -900 y la transaccion revierte (PA-INI-09, PA-INI-15).
            Guid nueva = _comoSistema.Crear(columnas);
            foreach (var correo in correos) _correos.ApuntarA(correo.Id, nueva);

            var limpieza = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_correonuevoenfinalizada", false },
                { "sanic_sinrespuestadesde", null }
            };
            _comoSistema.Actualizar(anteriorId, limpieza, null);

            Trazar("conversacion " + Texto(anteriorId) + ": nueva " + Texto(nueva) + " con " + correos.Count.ToString(CultureInfo.InvariantCulture) + " correos movidos");
            return new ResultadoDeIniciarConversacionNueva(nueva, correos.Count);
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

        // Una opcion ausente no puede ser Finalizada: se deja en -1 para que el par no coincida y caiga en -003.
        private static int Opcion(FilaDeDatos fila, string columna)
        {
            var opcion = Valor(fila, columna) as ValorDeOpcion;
            return opcion == null ? -1 : opcion.Valor;
        }
    }
}
