using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Cuota
{
    /// <summary>
    /// Las reglas de la Cuota (diseno/03 2.7, firma de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5d)"). Dominio puro: sin <c>Entity</c> ni SDK, sin estado (las listas son
    /// <see cref="ReadOnlyCollection{T}"/>), sin leer el reloj y con la identidad perezosa (0.3). Usa <c>Cambios</c>, <c>Estados</c>, <c>Columnas</c> y <c>ResultadoDeEvaluacion</c> sin reimplementarlos.
    /// </summary>
    public static class ReglasDeCuota
    {
        private static readonly IReadOnlyList<string> PreImagen = new ReadOnlyCollection<string>(new[]
        {
            "sanic_nombre", "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_fechapago", "sanic_montopagado", "sanic_arregloid", "statecode", "statuscode"
        });

        /// <summary>
        /// Las 9 columnas de la pre-imagen <c>PreImagen</c> de S-23 (seccion 5), en ese orden; es también el orden en que <c>CARP-CUO-001</c> nombra las columnas. Las siete que no son de pago son las
        /// vigiladas (cinco solo-sistema y los dos estados nativos); las dos de pago son las que la persona escribe.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagen { get { return PreImagen; } }

        /// <summary>
        /// S-23 (2.7, pasos 1 a 4). Permitido sin escribir nada (<c>Escribir</c> siempre vacío), o rechazado. En este orden:
        /// <list type="number">
        /// <item><b>Columnas cambiadas</b> entre las 9 (DC-01: presente en el <c>Target</c> Y distinto de la pre-imagen; una clave ausente de la pre-imagen es desconocida y cuenta como cambio; <c>""</c> es <c>null</c>).
        /// Las <c>fecha DO</c> (<c>sanic_fechavencimiento</c> y <c>sanic_fechapago</c>) se comparan por <c>.Date</c> cuando las dos son <c>DateTime</c> (Dev trunca la fecha al guardar, <c>RESULTADO.md</c> (2)); si una no lo
        /// es, vale <c>Cambios</c>. Los montos (<c>decimal</c>) por valor. Las claves del <c>Target</c> fuera de las 9 (<c>ownerid</c> lo rige S-11; <c>modifiedon</c> y el id primario) no cuentan. Ninguna ⇒ permite SIN
        /// consultar la identidad ni el arreglo.</item>
        /// <item><c>Identidad.EsEscrituraDelSistema</c> (una sola consulta) ⇒ permite (DC-03).</item>
        /// <item>Cambió alguna columna distinta de <c>sanic_fechapago</c> y <c>sanic_montopagado</c> (incluidos <c>statecode</c> y <c>statuscode</c>) ⇒ <c>CARP-CUO-001</c> nombrándolas todas, separadas por coma y espacio,
        /// en el orden de <see cref="ColumnasDeLaPreImagen"/>. Sin leer el arreglo.</item>
        /// <item>El arreglo (<c>sanic_arregloid</c> de la pre-imagen; <c>Lecturas.EstadoDelArreglo</c>, una vez) no es Vigente ⇒ <c>CARP-CUO-002</c>. Un arreglo ilegible (<c>null</c>) o sin referencia en la pre-imagen
        /// es "no Vigente": falla cerrado.</item>
        /// <item>Pago (DC-21), con los valores EFECTIVOS (el del <c>Target</c> si la clave viene, aunque sea <c>null</c>; si no, el de la pre-imagen; ausente cuenta como nulo): si solo uno de <c>sanic_fechapago</c> y
        /// <c>sanic_montopagado</c> tiene valor, o el monto tiene valor y no es mayor que cero ⇒ <c>CARP-CUO-003</c>. Sin tope superior. Ambos nulos pasa (borrar el pago).</item>
        /// </list>
        /// <b>DC-29 no aplica a la Cuota:</b> la cuota tiene solo los dos motivos por defecto (playbook, <c>motivos_estado = null</c>), así que reenviar el mismo <c>statecode</c> o <c>statuscode</c> no cambia nada
        /// en la plataforma y es "no cambió" (DC-01). Un estado distinto lo cambia solo SYSTEM.
        /// <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion Evaluar(PedidoDeCuota pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1 (2.7): columnas cambiadas entre las 9, sin consultar identidad ni arreglo.
            var cambiadas = new List<string>();
            foreach (var columna in PreImagen)
                if (Cambio(pedido, columna)) cambiadas.Add(columna);
            if (cambiadas.Count == 0) return ResultadoDeEvaluacion.Permitir();

            // Paso 2: DC-03, una sola pregunta.
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            // Paso 3: el oficial solo registra pagos (CARP-CUO-001), sin leer el arreglo.
            var noPermitidas = new List<string>();
            foreach (var columna in cambiadas)
                if (!EsColumnaDePago(columna)) noPermitidas.Add(columna);
            if (noPermitidas.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CUO_001.Crear(string.Join(", ", noPermitidas)));

            // Paso 4: decision 7 del 5d, el arreglo ilegible o sin referencia falla cerrado.
            var arregloId = ArregloDe(pedido.PreImagen);
            if (!arregloId.HasValue) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CUO_002.Crear());
            var estado = pedido.Lecturas.EstadoDelArreglo(arregloId.Value);
            if (!estado.HasValue || estado.Value != EstadoDeArreglo.Vigente) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CUO_002.Crear());

            // Paso 5: DC-21, valores efectivos; ambos o ninguno, monto mayor que cero, sin tope.
            var fecha = Efectivo(pedido, ColFechaPago);
            var monto = Efectivo(pedido, ColMontoPagado);
            var hayFecha = fecha != null;
            var hayMonto = monto != null;
            if (hayFecha != hayMonto) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CUO_003.Crear());
            if (hayMonto)
            {
                var valor = Monto(monto);
                if (!valor.HasValue || valor.Value <= 0m) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CUO_003.Crear());
            }
            return ResultadoDeEvaluacion.Permitir();
        }

        private const string ColFechaVencimiento = "sanic_fechavencimiento";
        private const string ColFechaPago = "sanic_fechapago";
        private const string ColMontoPagado = "sanic_montopagado";
        private const string ColArreglo = "sanic_arregloid";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColFechaTermino = "sanic_fechatermino";

        private static bool EsColumnaDePago(string columna)
        {
            return string.Equals(columna, ColFechaPago, StringComparison.Ordinal) || string.Equals(columna, ColMontoPagado, StringComparison.Ordinal);
        }

        // DC-01 puro (Cambios.Distinto), salvo las fecha DO, que se comparan por dia (cierre 5c, 2.7 decision 4 del 5d).
        private static bool Cambio(PedidoDeCuota pedido, string columna)
        {
            if (string.Equals(columna, ColFechaVencimiento, StringComparison.Ordinal) || string.Equals(columna, ColFechaPago, StringComparison.Ordinal))
            {
                object nueva, previa;
                if (pedido.Target.TryGetValue(columna, out nueva) && pedido.PreImagen.TryGetValue(columna, out previa) && nueva is DateTime && previa is DateTime)
                    return ((DateTime)nueva).Date != ((DateTime)previa).Date;
            }
            return Cambios.Distinto(pedido.Target, pedido.PreImagen, columna);
        }

        // El valor efectivo: el del Target si la clave viene (aunque sea null); si no, el de la pre-imagen; ausente cuenta como nulo.
        private static object Efectivo(PedidoDeCuota pedido, string columna)
        {
            object valor;
            if (pedido.Target.TryGetValue(columna, out valor)) return Vacio(valor);
            return pedido.PreImagen.TryGetValue(columna, out valor) ? Vacio(valor) : null;
        }

        private static object Vacio(object valor)
        {
            var texto = valor as string;
            return texto != null && texto.Length == 0 ? null : valor;
        }

        private static decimal? Monto(object valor)
        {
            if (valor is decimal) return (decimal)valor;
            var dinero = valor as ValorDeDinero;
            return dinero != null ? (decimal?)dinero.Valor : null;
        }

        private static Guid? ArregloDe(IReadOnlyDictionary<string, object> preImagen)
        {
            object valor;
            if (!preImagen.TryGetValue(ColArreglo, out valor)) return null;
            var referencia = valor as ReferenciaDeEntidad;
            if (referencia != null) return referencia.Id;
            return valor is Guid ? (Guid?)(Guid)valor : null;
        }

        /// <summary>
        /// Lo que S-24 escribe en el arreglo al terminarlo (2.7, paso 3): <c>statecode</c> y <c>statuscode</c> del estado Terminado (<c>ValorDeOpcion</c>, de <c>Estados.De(EstadoDeArreglo.Terminado)</c>, siempre las dos,
        /// 0.7) y <c>sanic_fechatermino</c> = <paramref name="ahoraUtc"/>. Exactamente esas tres claves, de solo lectura de verdad. <paramref name="ahoraUtc"/> con <see cref="DateTimeKind"/> distinto de Utc ⇒
        /// <see cref="ArgumentException"/> (el contrato de <c>IReloj.AhoraUtc</c>).
        /// </summary>
        public static IReadOnlyDictionary<string, object> ColumnasAlTerminarElArreglo(DateTime ahoraUtc)
        {
            if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La hora debe ser UTC (contrato de IReloj.AhoraUtc).", nameof(ahoraUtc));
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            var columnas = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ColEstado, new ValorDeOpcion(terminado.StateCode) },
                { ColMotivoDeEstado, new ValorDeOpcion(terminado.StatusCode) },
                { ColFechaTermino, ahoraUtc }
            };
            return new ReadOnlyDictionary<string, object>(columnas);
        }
    }
}
